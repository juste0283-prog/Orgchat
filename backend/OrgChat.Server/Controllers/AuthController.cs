using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Constants;
using OrgChat.Core.Contracts.Auth;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Entities;
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
    private readonly IConfiguration _configuration;

    public AuthController(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IUserRoleService userRoleService,
        IAuditService auditService,
        IConfiguration configuration)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _userRoleService = userRoleService;
        _auditService = auditService;
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

    /// <summary>
    /// Inscription libre. Désactivée par défaut : seuls les admins créent les
    /// comptes. Ne reste active que si OrgChat:EnableSelfRegistration=true
    /// (utilisé uniquement pour les tests/intégrations internes).
    /// </summary>
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
            Status = Core.Enums.AccountStatus.Active,
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
                : user.Status == Core.Enums.AccountStatus.Disabled ? "Compte désactivé."
                : "Compte suspendu. Contactez votre administrateur.";
            return await RejectLoginAsync(user.Id, ip, ua, reason, reason);
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            return await RejectLoginAsync(user.Id, ip, ua, failureMessage, "Mot de passe incorrect.");

        // Révocation automatique des sessions expirées pour cet utilisateur.
        var expiredSessions = _db.Sessions.Where(s => s.UserId == user.Id && s.ExpiresAt < DateTime.UtcNow);
        _db.Sessions.RemoveRange(expiredSessions);

        // Session persistée : révocable côté serveur, identifiable par jti.
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
