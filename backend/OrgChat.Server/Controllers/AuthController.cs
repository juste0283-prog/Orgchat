using Microsoft.AspNetCore.Authorization;
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
    /// Page HTML autonome ouverte lorsqu'un utilisateur clique sur un lien
    /// d'activation reçu par e-mail ou messagerie.
    /// </summary>
    [HttpGet("activate")]
    [AllowAnonymous]
    public IActionResult ActivatePage()
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return Content(ActivationPageHtml, "text/html; charset=utf-8");
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

    private const string ActivationPageHtml = """
<!doctype html>
<html lang="fr">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Activation du compte · OrgChat</title>
  <style>
    :root {
      --bg: #f5f7fb;
      --surface: #ffffff;
      --text: #122033;
      --muted: #68758a;
      --accent: #1b65fc;
      --border: #dfe6f1;
      --success: #0f9f6e;
      --danger: #d64545;
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 24px;
      background:
        radial-gradient(circle at 15% 10%, rgba(27, 101, 252, .12), transparent 32rem),
        radial-gradient(circle at 85% 90%, rgba(15, 159, 110, .10), transparent 30rem),
        var(--bg);
      color: var(--text);
      font-family: "Segoe UI", system-ui, -apple-system, sans-serif;
    }
    .card {
      width: min(440px, 100%);
      padding: 34px;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: 20px;
      box-shadow: 0 24px 60px rgba(16, 34, 60, .12);
    }
    .brand {
      margin-bottom: 28px;
      color: #0d1f36;
      font-size: 15px;
      font-weight: 700;
      letter-spacing: .2px;
    }
    .brand span { color: var(--muted); font-weight: 500; }
    h1 { margin: 0 0 8px; font-size: 24px; line-height: 1.2; }
    p { margin: 0; color: var(--muted); font-size: 14px; line-height: 1.55; }
    label {
      display: block;
      margin: 18px 0 7px;
      color: var(--muted);
      font-size: 13px;
      font-weight: 600;
    }
    input {
      width: 100%;
      height: 45px;
      padding: 0 14px;
      border: 1px solid var(--border);
      border-radius: 12px;
      background: #fbfcfe;
      color: var(--text);
      font: inherit;
      outline: none;
    }
    input:focus {
      border-color: var(--accent);
      box-shadow: 0 0 0 3px rgba(27, 101, 252, .13);
    }
    button {
      width: 100%;
      height: 46px;
      margin-top: 20px;
      border: 0;
      border-radius: 12px;
      background: var(--accent);
      color: #fff;
      font: inherit;
      font-weight: 700;
      cursor: pointer;
    }
    button:disabled { cursor: wait; opacity: .65; }
    .message {
      display: none;
      margin-top: 16px;
      padding: 11px 13px;
      border-radius: 10px;
      font-size: 13px;
      line-height: 1.45;
    }
    .message.error {
      display: block;
      border: 1px solid #f3c7c7;
      background: #fff1f1;
      color: var(--danger);
    }
    .check {
      display: grid;
      place-items: center;
      width: 54px;
      height: 54px;
      margin-bottom: 18px;
      border-radius: 50%;
      background: #e9f8f2;
      color: var(--success);
      font-size: 27px;
      font-weight: 700;
    }
    .hidden { display: none; }
  </style>
</head>
<body>
  <main class="card">
    <div class="brand">OrgChat <span>· Activation</span></div>

    <section id="form">
      <h1>Activez votre compte</h1>
      <p>Choisissez le mot de passe qui protégera votre compte OrgChat.</p>
      <form id="activation-form">
        <label for="password">Nouveau mot de passe</label>
        <input id="password" type="password" minlength="8" autocomplete="new-password" required>
        <label for="confirm">Confirmer le mot de passe</label>
        <input id="confirm" type="password" minlength="8" autocomplete="new-password" required>
        <div id="message" class="message" role="alert"></div>
        <button id="submit" type="submit">Activer mon compte</button>
      </form>
    </section>

    <section id="success" class="hidden">
      <div class="check">✓</div>
      <h1>Compte activé</h1>
      <p id="success-text">Votre compte est prêt. Vous pouvez fermer cette page et vous connecter dans l'application OrgChat.</p>
    </section>
  </main>

  <script>
    (() => {
      const token = new URLSearchParams(window.location.search).get('token') || '';
      const form = document.getElementById('activation-form');
      const password = document.getElementById('password');
      const confirm = document.getElementById('confirm');
      const message = document.getElementById('message');
      const submit = document.getElementById('submit');

      const showError = (text) => {
        message.textContent = text;
        message.className = 'message error';
      };

      if (!token) {
        showError("Lien d'activation incomplet. Demandez un nouveau lien à votre administrateur.");
        submit.disabled = true;
      }

      form.addEventListener('submit', async (event) => {
        event.preventDefault();

        if (password.value.length < 8) {
          showError('Le mot de passe doit contenir au moins 8 caractères.');
          return;
        }

        if (password.value !== confirm.value) {
          showError('Les deux mots de passe ne correspondent pas.');
          return;
        }

        message.className = 'message';
        submit.disabled = true;
        submit.textContent = 'Activation…';

        try {
          const response = await fetch(window.location.pathname, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ token, newPassword: password.value })
          });
          const data = await response.json().catch(() => ({}));

          if (!response.ok) {
            throw new Error(data.error || "Lien d'activation invalide ou expiré.");
          }

          document.getElementById('form').classList.add('hidden');
          document.getElementById('success').classList.remove('hidden');
          if (data.username) {
            document.getElementById('success-text').textContent =
              `Le compte @${data.username} est activé. Vous pouvez fermer cette page et vous connecter dans l'application OrgChat.`;
          }
        } catch (error) {
          showError(error.message || "Impossible d'activer le compte.");
          submit.disabled = false;
          submit.textContent = 'Activer mon compte';
        }
      });
    })();
  </script>
</body>
</html>
""";
}
