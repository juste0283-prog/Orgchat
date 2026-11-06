using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Constants;
using OrgChat.Core.Contracts.Auth;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Entities;
using OrgChat.Core.Enums;
using OrgChat.Infrastructure.Data;
using System.Security.Claims;

namespace OrgChat.Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IUserRoleService _userRoleService;
    private readonly IAuditService _auditService;
    private readonly IInvitationService _invitationService;
    private readonly IConfiguration _configuration;

    public AuthController(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IUserRoleService userRoleService,
        IAuditService auditService,
        IInvitationService invitationService,
        IConfiguration configuration)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _userRoleService = userRoleService;
        _auditService = auditService;
        _invitationService = invitationService;
        _configuration = configuration;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdClaim != null ? int.Parse(userIdClaim) : 0;
    }

    private (string? Ip, string? UserAgent) GetClientInfo() =>
        (HttpContext.Connection.RemoteIpAddress?.ToString(),
         Request.Headers.UserAgent.ToString());

    private bool IsSelfRegistrationEnabled() =>
        bool.TryParse(_configuration["OrgChat:EnableSelfRegistration"], out var enabled) && enabled;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!IsSelfRegistrationEnabled())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "La création libre de comptes est désactivée. Demandez à un administrateur de créer votre compte.",
            });
        }

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Tous les champs sont requis." });
        }

        if (await _db.Users.AnyAsync(u => u.Username == request.Username))
            return BadRequest(new { error = "Ce nom d'utilisateur est déjà utilisé." });

        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest(new { error = "Cet e-mail est déjà utilisé." });

        var defaultOrgId = await _db.Organizations
            .Where(o => o.IsActive)
            .OrderBy(o => o.Id)
            .Select(o => o.Id)
            .FirstOrDefaultAsync();
        if (defaultOrgId == 0)
            return Problem("Aucune organisation par défaut n'est configurée.");

        var user = new User
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Username.Trim() : request.DisplayName.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            IsActive = true,
            Status = AccountStatus.Active,
            OrganizationId = defaultOrgId,
            ActivatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await _userRoleService.AssignDefaultRoleAsync(user);
        await _db.SaveChangesAsync();

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(user.Id, AuditActions.AccountCreated, "User",
            user.Id.ToString(), "Source : auto-inscription (env de test)", "Success", ip, ua);

        return Ok(await BuildAuthResponseAsync(user, ip, ua));
    }

    /// <summary>
    /// Activation d'un compte via jeton à usage unique (première connexion)
    /// ou réinitialisation via jeton émis par un administrateur.
    /// Le jeton est hashé, expire et ne peut être utilisé qu'une seule fois.
    /// </summary>
    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateAccountRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { error = "Le jeton d'activation est requis." });

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { error = "Le nouveau mot de passe est requis." });

        if (request.NewPassword.Length < 8)
            return BadRequest(new { error = "Le mot de passe doit contenir au moins 8 caractères." });

        var (ip, ua) = GetClientInfo();

        // On valide l'expiration/usage avant consommation pour ne jamais
        // consommer un jeton invalide. ValidateAndConsume marque UsedAt.
        var invitation = await _invitationService.ValidateAndConsumeAsync(request.Token, ip);
        if (invitation == null)
        {
            return BadRequest(new { error = "Lien d'activation invalide ou expiré. Contactez votre administrateur pour un nouveau lien." });
        }

        var user = await _userRoleService.GetUserWithRolesAsync(invitation.UserId);
        if (user == null)
            return BadRequest(new { error = "Compte introuvable pour ce jeton." });

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        user.IsActive = true;
        user.Status = AccountStatus.Active;
        user.SuspendedUntil = null;
        user.SuspensionReason = null;
        user.ActivatedAt ??= DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var action = invitation.Type == InvitationType.PasswordReset
            ? AuditActions.PasswordReset
            : AuditActions.AccountActivated;

        await _auditService.LogAsync(user.Id, action, "User", user.Id.ToString(),
            invitation.Type == InvitationType.PasswordReset ? "Réinitialisation via jeton" : "Activation première connexion",
            "Success", ip, ua);
        await _auditService.LogUserActivityAsync(user.Id,
            invitation.Type == InvitationType.PasswordReset ? ActivityTypes.Login : ActivityTypes.AccountActivated,
            null, ip, ua);

        // Connexion immédiate : première connexion = session créée.
        return Ok(await CreateSessionAndBuildAuthAsync(user, ip, ua));
    }

    /// <summary>Vérification sans consommation d'un jeton (écran d'activation).</summary>
    [HttpPost("invitations/validate")]
    public async Task<IActionResult> ValidateInvitation([FromBody] ValidateInvitationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { error = "Le jeton est requis." });

        var tokenHash = OrgChat.Infrastructure.Services.InvitationService.HashToken(request.Token.Trim());
        var invitation = await _db.Invitations
            .Include(i => i.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash);

        if (invitation == null)
            return Ok(new ValidateInvitationResponse { IsValid = false, Error = "Lien invalide." });

        if (invitation.UsedAt != null)
            return Ok(new ValidateInvitationResponse { IsValid = false, Error = "Lien déjà utilisé." });

        if (invitation.ExpiresAt <= DateTime.UtcNow)
            return Ok(new ValidateInvitationResponse { IsValid = false, Error = "Lien expiré. Contactez votre administrateur." });

        return Ok(new ValidateInvitationResponse
        {
            IsValid = true,
            Email = invitation.User.Email,
            DisplayName = invitation.User.DisplayName,
            ExpiresAt = invitation.ExpiresAt,
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return Unauthorized(new { error = "Identifiants invalides." });

        var (ip, ua) = GetClientInfo();
        var failureMessage = "Identifiants invalides.";

        var user = await _userRoleService.GetUserWithRolesAsync(
            await _db.Users
                .Where(u => u.Username == request.Username)
                .Select(u => u.Id)
                .FirstOrDefaultAsync());

        if (user == null)
            return await RejectLoginAsync(null, ip, ua, failureMessage, "Utilisateur inconnu.");

        if (!user.IsLoginPermitted(DateTime.UtcNow))
        {
            var reason = !user.IsActive ? "Compte désactivé ou supprimé."
                : user.Status == AccountStatus.Disabled ? "Compte désactivé."
                : "Compte suspendu. Contactez votre administrateur.";
            return await RejectLoginAsync(user.Id, ip, ua, reason, reason);
        }

        if (user.MustChangePassword || user.ActivatedAt == null)
        {
            return await RejectLoginAsync(user.Id, ip, ua,
                "Votre compte n'est pas encore activé. Utilisez le lien d'activation fourni par votre administrateur.",
                "Compte en attente d'activation.");
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            return await RejectLoginAsync(user.Id, ip, ua, failureMessage, "Mot de passe incorrect.");

        var expiredSessions = _db.Sessions.Where(s => s.UserId == user.Id && s.ExpiresAt < DateTime.UtcNow);
        _db.Sessions.RemoveRange(expiredSessions);

        var tokenId = Guid.NewGuid().ToString("N");
        var session = new Session
        {
            UserId = user.Id,
            TokenId = tokenId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(120),
            IpAddress = ip,
            UserAgent = ua?.Length > 256 ? ua[..256] : ua,
        };
        _db.Sessions.Add(session);

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(user.Id, AuditActions.LoggedIn, "User", user.Id.ToString(), null, "Success", ip, ua);
        await _auditService.LogUserActivityAsync(user.Id, ActivityTypes.Login, null, ip, ua);

        return Ok(await BuildAuthResponseAsync(user, ip, ua, tokenId));
    }

    [HttpPost("logout")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
            return NoContent();

        var tokenId = User.FindFirst("jti")?.Value ?? User.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
        if (!string.IsNullOrEmpty(tokenId))
        {
            var session = await _db.Sessions.FirstOrDefaultAsync(s => s.TokenId == tokenId);
            if (session != null)
                session.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(userId, AuditActions.LoggedOut, "User", userId.ToString(), null, "Success", ip, ua);
        await _auditService.LogUserActivityAsync(userId, ActivityTypes.Logout, null, ip, ua);

        return NoContent();
    }

    private async Task<IActionResult> RejectLoginAsync(int? userId, string? ip, string? ua, string publicMessage, string auditDetail)
    {
        if (userId != null)
        {
            await _auditService.LogAsync(userId, AuditActions.LoginFailed, "User",
                userId.ToString(), auditDetail, "Denied", ip, ua);
            await _auditService.LogUserActivityAsync(userId.Value, ActivityTypes.LoginFailed, auditDetail, ip, ua);
        }

        return Unauthorized(new { error = publicMessage });
    }

    private async Task<AuthResponse> CreateSessionAndBuildAuthAsync(User user, string? ip, string? ua)
    {
        var tokenId = Guid.NewGuid().ToString("N");
        var session = new Session
        {
            UserId = user.Id,
            TokenId = tokenId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(120),
            IpAddress = ip,
            UserAgent = ua?.Length > 256 ? ua[..256] : ua,
        };
        _db.Sessions.Add(session);
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await BuildAuthResponseAsync(user, ip, ua, tokenId);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(User user, string? ip, string? ua, string? tokenId = null)
    {
        var token = _tokenService.GenerateToken(user, tokenId ?? Guid.NewGuid().ToString("N"));

        return new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = _userRoleService.GetPrimaryRole(user),
        };
    }
}
