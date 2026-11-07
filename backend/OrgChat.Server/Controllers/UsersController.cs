using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Constants;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Contracts.Users;
using OrgChat.Core.Entities;
using OrgChat.Core.Enums;
using OrgChat.Infrastructure.Data;
using System.Security.Claims;

namespace OrgChat.Server.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private const int MinimumPasswordLength = 8;
    private const string AdminRoles = "SuperAdmin,OrgAdmin";

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserRoleService _userRoleService;
    private readonly IAuditService _auditService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        IUserRoleService userRoleService,
        IAuditService auditService,
        ILogger<UsersController> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _userRoleService = userRoleService;
        _auditService = auditService;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdClaim != null ? int.Parse(userIdClaim) : 0;
    }

    private bool IsCurrentUserAdmin() =>
        User.IsInRole(RoleKeys.SuperAdmin) || User.IsInRole(RoleKeys.OrgAdmin);

    private (string? Ip, string? UserAgent) GetClientInfo() =>
        (HttpContext.Connection.RemoteIpAddress?.ToString(),
         Request.Headers.UserAgent.ToString());

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var user = await _userRoleService.GetUserWithRolesAsync(GetCurrentUserId());
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        return Ok(MapUser(user));
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.Username)
            .ToListAsync();

        return Ok(users.Select(MapUser));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _userRoleService.GetUserWithRolesAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        return Ok(MapUser(user));
    }

    [HttpPost]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Le nom d'utilisateur, l'e-mail et le mot de passe sont requis." });
        }

        if (request.Password.Length < MinimumPasswordLength)
            return BadRequest(new { error = $"Le mot de passe doit contenir au moins {MinimumPasswordLength} caractères." });

        var roleKey = string.IsNullOrWhiteSpace(request.Role) ? RoleKeys.User : request.Role;
        if (!RoleKeys.IsValid(roleKey))
            return BadRequest(new { error = "Rôle invalide." });

        // Seul le SuperAdmin peut désigner un administrateur ou un superviseur
        // (et donc créer leurs comptes). Les OrgAdmin ne peuvent que créer
        // des utilisateurs simples ou des responsables de département.
        if (!CanAssignRole(roleKey))
            return Forbid();

        var username = request.Username.Trim();
        var email = request.Email.Trim();

        if (await _db.Users.AnyAsync(u => u.Username == username))
            return BadRequest(new { error = "Ce nom d'utilisateur est déjà utilisé." });

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return BadRequest(new { error = "Cet e-mail est déjà utilisé." });

        var admin = await _userRoleService.GetUserWithRolesAsync(GetCurrentUserId());
        if (admin == null)
            return Unauthorized();

        var user = new User
        {
            Username = username,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? username : request.DisplayName.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            IsActive = true,
            Status = AccountStatus.Active,
            OrganizationId = admin.OrganizationId,
            ActivatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await _userRoleService.AssignDefaultRoleAsync(user);
        if (roleKey != RoleKeys.User)
        {
            await _userRoleService.SetOrganizationRoleAsync(user, roleKey);
        }
        else
        {
            await _db.SaveChangesAsync();
        }

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(admin.Id, AuditActions.AccountCreated, "User",
            user.Id.ToString(), $"Rôle : {roleKey}", "Success", ip, ua);

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, MapUser(
            await _userRoleService.GetUserWithRolesAsync(user.Id)));
    }

    private bool CanAssignRole(string roleKey)
    {
        // Seul le SuperAdmin peut désigner un administrateur ou un superviseur.
        if (roleKey is RoleKeys.SuperAdmin or RoleKeys.OrgAdmin or RoleKeys.Supervisor)
            return User.IsInRole(RoleKeys.SuperAdmin);

        // Responsable de département et utilisateur : SuperAdmin ou OrgAdmin.
        return User.IsInRole(RoleKeys.SuperAdmin) || User.IsInRole(RoleKeys.OrgAdmin);
    }

    [HttpPut("{id}/role")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateUserRoleRequest request)
    {
        var roleKey = string.IsNullOrWhiteSpace(request.Role) ? RoleKeys.User : request.Role;
        if (!RoleKeys.IsValid(roleKey))
            return BadRequest(new { error = "Rôle invalide." });

        if (!CanAssignRole(roleKey))
            return Forbid();

        var currentUserId = GetCurrentUserId();
        var user = await _userRoleService.GetUserWithRolesAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        var currentRole = _userRoleService.GetPrimaryRole(user);
        var isSuperAdmin = User.IsInRole(RoleKeys.SuperAdmin);
        if (currentRole == RoleKeys.SuperAdmin && !isSuperAdmin)
            return Forbid();
        if (roleKey == RoleKeys.SuperAdmin && !isSuperAdmin)
            return Forbid();

        // Un OrgAdmin ne peut ni rétrograder ni toucher un compte
        // administrateur / superviseur (rôles désignés par le SuperAdmin).
        if (!isSuperAdmin && currentRole is RoleKeys.OrgAdmin or RoleKeys.Supervisor or RoleKeys.SuperAdmin)
            return Forbid();

        if (id == currentUserId && roleKey != RoleKeys.OrgAdmin && roleKey != RoleKeys.SuperAdmin)
            return BadRequest(new { error = "Vous ne pouvez pas retirer votre propre rôle administrateur." });

        if (currentRole is RoleKeys.OrgAdmin or RoleKeys.SuperAdmin &&
            roleKey is not (RoleKeys.OrgAdmin or RoleKeys.SuperAdmin))
        {
            var adminCount = await _db.UserRoles
                .Where(ur => ur.Role.Key == RoleKeys.OrgAdmin || ur.Role.Key == RoleKeys.SuperAdmin)
                .Select(ur => ur.UserId)
                .Distinct()
                .CountAsync();
            if (adminCount <= 1)
                return BadRequest(new { error = "Au moins un administrateur actif est requis." });
        }

        var oldRole = currentRole;
        await _userRoleService.SetOrganizationRoleAsync(user, roleKey);

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(currentUserId, AuditActions.AccountUpdated, "User",
            user.Id.ToString(), $"Rôle modifié : {oldRole} → {roleKey}", "Success", ip, ua);

        return Ok(MapUser(await _userRoleService.GetUserWithRolesAsync(user.Id)));
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangeMyPassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { error = "Le mot de passe actuel et le nouveau mot de passe sont requis." });
        }

        if (request.NewPassword.Length < MinimumPasswordLength)
            return BadRequest(new { error = $"Le nouveau mot de passe doit contenir au moins {MinimumPasswordLength} caractères." });

        var user = await _db.Users.FindAsync(GetCurrentUserId());
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { error = "Le mot de passe actuel est incorrect." });

        if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
            return BadRequest(new { error = "Le nouveau mot de passe doit être différent de l'ancien." });

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        await _db.SaveChangesAsync();

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(user.Id, AuditActions.PasswordReset, "User",
            user.Id.ToString(), "Changement de mot de passe personnel", "Success", ip, ua);

        return NoContent();
    }

    [HttpPut("{id}/password")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword.Length < MinimumPasswordLength)
        {
            return BadRequest(new { error = $"Le nouveau mot de passe doit contenir au moins {MinimumPasswordLength} caractères." });
        }

        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _db.SaveChangesAsync();

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.PasswordReset, "User",
            user.Id.ToString(), "Réinitialisation par un administrateur", "Success", ip, ua);

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (id == currentUserId)
            return BadRequest(new { error = "Vous ne pouvez pas supprimer votre propre compte." });

        var user = await _userRoleService.GetUserWithRolesAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        if (_userRoleService.GetPrimaryRole(user) == RoleKeys.SuperAdmin && !User.IsInRole(RoleKeys.SuperAdmin))
            return Forbid();

        // Suppression logique : les messages restent cohérents, l'identité est anonymisée.
        user.Username = $"deleted_{user.Id}";
        user.Email = $"deleted_{user.Id}@invalid.local";
        user.DisplayName = "Compte supprimé";
        user.PasswordHash = _passwordHasher.Hash(Guid.NewGuid().ToString("N"));
        user.IsActive = false;
        user.Status = AccountStatus.Disabled;
        await _db.SaveChangesAsync();

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(currentUserId, AuditActions.AccountDeleted, "User",
            user.Id.ToString(), "Compte anonymisé, désactivé", "Success", ip, ua);

        return NoContent();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        var currentUserId = GetCurrentUserId();
        var isAdmin = IsCurrentUserAdmin();

        if (currentUserId != id && !isAdmin)
            return Forbid();

        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        var changes = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            user.DisplayName = request.DisplayName.Trim();
            changes.Add("nom affiché");
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var emailExists = await _db.Users.AnyAsync(u => u.Email == request.Email.Trim() && u.Id != id);
            if (emailExists)
                return BadRequest(new { error = "Cet e-mail est déjà utilisé." });
            user.Email = request.Email.Trim();
            changes.Add("e-mail");
        }

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(currentUserId, AuditActions.AccountUpdated, "User",
            user.Id.ToString(), changes.Count > 0 ? $"Modifications : {string.Join(", ", changes)}" : null,
            "Success", GetClientInfo().Ip, GetClientInfo().UserAgent);

        var loaded = await _userRoleService.GetUserWithRolesAsync(user.Id);
        return Ok(MapUser(loaded));
    }

    [HttpPut("{id}/deactivate")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> DeactivateUser(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == id)
            return BadRequest(new { error = "Vous ne pouvez pas désactiver votre propre compte." });

        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        user.IsActive = false;
        await _db.RevokeAllSessionsAsync(id);

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(currentUserId, AuditActions.AccountDeactivated, "User",
            user.Id.ToString(), "Compte désactivé, sessions révoquées", "Success", ip, ua);

        return NoContent();
    }

    [HttpPut("{id}/reactivate")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> ReactivateUser(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        user.IsActive = true;
        user.Status = AccountStatus.Active;
        user.SuspendedUntil = null;
        user.SuspensionReason = null;
        await _db.SaveChangesAsync();

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.AccountReactivated, "User",
            user.Id.ToString(), "Compte réactivé", "Success", ip, ua);

        return NoContent();
    }

    private static UserDto MapUser(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Role = user.UserRoles.Count == 0
            ? RoleKeys.User
            : RoleKeys.HighestRole(user.UserRoles.Select(ur => ur.Role.Key)),
        IsActive = user.IsActive
    };
}
