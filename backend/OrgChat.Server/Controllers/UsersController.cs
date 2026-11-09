using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Constants;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Contracts.Users;
using OrgChat.Core.Entities;
using OrgChat.Core.Enums;
using OrgChat.Infrastructure.Data;
using OrgChat.Server.Hubs;
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
    private readonly IInvitationService _invitationService;
    private readonly IEmailService _emailService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        IUserRoleService userRoleService,
        IAuditService auditService,
        IInvitationService invitationService,
        IEmailService emailService,
        ILogger<UsersController> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _userRoleService = userRoleService;
        _auditService = auditService;
        _invitationService = invitationService;
        _emailService = emailService;
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
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search = null,
        [FromQuery] int? departmentId = null,
        [FromQuery] string? role = null,
        [FromQuery] string? status = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 0)
    {
        var query = _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Include(u => u.Department)
            .Include(u => u.DepartmentMembers)
            .ThenInclude(m => m.Department)
            .AsQueryable();

        // Isolation stricte : un non-SuperAdmin ne voit que son organisation.
        if (!User.IsSuperAdmin())
        {
            var orgId = User.GetUserOrganizationId();
            if (orgId != 0)
                query = query.Where(u => u.OrganizationId == orgId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.Username.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                u.DisplayName.ToLower().Contains(term) ||
                (u.FirstName != null && u.FirstName.ToLower().Contains(term)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(term)));
        }

        if (departmentId != null)
        {
            query = query.Where(u =>
                u.DepartmentId == departmentId.Value ||
                u.DepartmentMembers.Any(m => m.DepartmentId == departmentId.Value));
        }

        if (!string.IsNullOrWhiteSpace(role) && RoleKeys.IsValid(role))
            query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Key == role));

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<AccountStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(u => u.Status == parsedStatus);
        }

        if (isActive != null)
            query = query.Where(u => u.IsActive == isActive.Value);

        query = query.OrderBy(u => u.Username);

        if (page > 0 && pageSize > 0)
        {
            var size = Math.Clamp(pageSize, 1, 200);
            query = query.Skip((page - 1) * size).Take(size);
        }

        var users = await query.ToListAsync();
        return Ok(users.Select(MapUser));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _userRoleService.GetUserWithRolesAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        if (!User.BelongsToOrganization(user.OrganizationId))
            return NotFound(new { error = "Utilisateur introuvable." });

        await _db.Entry(user).Reference(u => u.Department).LoadAsync();
        return Ok(MapUser(user));
    }

    [HttpPost]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var roleKey = string.IsNullOrWhiteSpace(request.Role) ? RoleKeys.User : request.Role;
        if (!RoleKeys.IsValid(roleKey))
            return BadRequest(new { error = "Rôle invalide." });

        if (!CanAssignRole(roleKey))
            return Forbid();

        var admin = await _userRoleService.GetUserWithRolesAsync(GetCurrentUserId());
        if (admin == null)
            return Unauthorized();

        // Organisation cible : SuperAdmin peut cibler une organisation,
        // sinon l'organisation courante de l'administrateur.
        int organizationId;
        if (User.IsInRole(RoleKeys.SuperAdmin) && request.OrganizationId != null)
        {
            organizationId = request.OrganizationId.Value;
            if (!await _db.Organizations.AnyAsync(o => o.Id == organizationId && o.IsActive))
                return BadRequest(new { error = "Organisation cible introuvable ou inactive." });
        }
        else
        {
            organizationId = admin.OrganizationId;
        }

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "L'adresse e-mail professionnelle est requise." });

        var email = request.Email.Trim();
        if (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
            return BadRequest(new { error = "Saisissez une adresse e-mail valide." });

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return BadRequest(new { error = "Cet e-mail est déjà utilisé." });

        if (request.DepartmentId != null)
        {
            var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == request.DepartmentId.Value);
            if (department == null || department.OrganizationId != organizationId || !department.IsActive)
                return BadRequest(new { error = "Département introuvable dans cette organisation." });
        }

        var firstName = request.FirstName?.Trim() ?? string.Empty;
        var lastName = request.LastName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName) &&
            string.IsNullOrWhiteSpace(request.DisplayName) && string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new { error = "Le nom et le prénom sont requis." });
        }

        var username = string.IsNullOrWhiteSpace(request.Username)
            ? await GenerateUniqueUsernameAsync(firstName, lastName, email)
            : request.Username.Trim();

        if (await _db.Users.AnyAsync(u => u.Username == username))
            return BadRequest(new { error = "Ce nom d'utilisateur est déjà utilisé." });

        var displayName = !string.IsNullOrWhiteSpace(request.DisplayName)
            ? request.DisplayName.Trim()
            : !string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName)
                ? $"{firstName} {lastName}".Trim()
                : username;

        var hasDirectPassword = !string.IsNullOrWhiteSpace(request.Password);
        if (hasDirectPassword && request.Password.Length < MinimumPasswordLength)
        {
            return BadRequest(new { error = $"Le mot de passe doit contenir au moins {MinimumPasswordLength} caractères." });
        }

        var isActive = request.IsActive;

        var user = new User
        {
            Username = username,
            Email = email,
            DisplayName = displayName,
            FirstName = firstName,
            LastName = lastName,
            JobTitle = request.JobTitle?.Trim() ?? string.Empty,
            DepartmentId = request.DepartmentId,
            PasswordHash = hasDirectPassword
                ? _passwordHasher.Hash(request.Password)
                : _passwordHasher.Hash(Guid.NewGuid().ToString("N")),
            IsActive = isActive,
            Status = isActive ? AccountStatus.Active : AccountStatus.Disabled,
            OrganizationId = organizationId,
            MustChangePassword = !hasDirectPassword,
            ActivatedAt = hasDirectPassword ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await _userRoleService.AssignDefaultRoleAsync(user);
        if (roleKey != RoleKeys.User)
            await _userRoleService.SetOrganizationRoleAsync(user, roleKey);
        else
            await _db.SaveChangesAsync();

        if (request.DepartmentId != null)
        {
            _db.DepartmentMembers.Add(new DepartmentMember
            {
                DepartmentId = request.DepartmentId.Value,
                UserId = user.Id,
                IsPrimary = true,
                JoinedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync();
        }

        string? activationToken = null;
        string? activationLink = null;
        DateTime? invitationExpiresAt = null;

        if (!hasDirectPassword)
        {
            activationToken = await _invitationService.CreateInvitationAsync(
                user.Id, InvitationType.AccountActivation, admin.Id);
            invitationExpiresAt = DateTime.UtcNow.AddHours(_invitationService.ExpirationHours);
            activationLink = $"/api/auth/activate?token={activationToken}";

            if (_emailService.IsConfigured)
            {
                try
                {
                    await _emailService.SendActivationInvitationAsync(user, activationLink);
                }
                catch
                {
                    // Best-effort : le lien copiable reste disponible dans l'UI.
                }
            }
        }

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(admin.Id, AuditActions.AccountCreated, "User",
            user.Id.ToString(),
            hasDirectPassword ? $"Rôle : {roleKey} (création directe)"
                : $"Rôle : {roleKey} (invitation d'activation)",
            "Success", ip, ua);

        var loaded = await _userRoleService.GetUserWithRolesAsync(user.Id);
        var dto = MapUser(loaded);

        if (hasDirectPassword)
        {
            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, dto);
        }

        var result = new CreatedUserDto
        {
            Id = dto.Id,
            Username = dto.Username,
            Email = dto.Email,
            DisplayName = dto.DisplayName,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            JobTitle = dto.JobTitle,
            DepartmentId = dto.DepartmentId,
            DepartmentName = dto.DepartmentName,
            OrganizationId = dto.OrganizationId,
            Role = dto.Role,
            IsActive = dto.IsActive,
            Status = dto.Status,
            SuspendedUntil = dto.SuspendedUntil,
            SuspensionReason = dto.SuspensionReason,
            MustChangePassword = dto.MustChangePassword,
            ActivatedAt = dto.ActivatedAt,
            LastLoginAt = dto.LastLoginAt,
            IsOnline = dto.IsOnline,
            ActivationToken = activationToken,
            ActivationLink = activationLink,
            InvitationExpiresAt = invitationExpiresAt,
        };

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, result);
    }

    private async Task<string> GenerateUniqueUsernameAsync(string firstName, string lastName, string email)
    {
        static string NormalizePart(string value)
        {
            var formD = value.Normalize(System.Text.NormalizationForm.FormD);
            var cleaned = new string(formD
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
            return cleaned;
        }

        var baseName = NormalizePart(firstName) + (string.IsNullOrWhiteSpace(lastName) ? string.Empty : "." + NormalizePart(lastName));
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = NormalizePart(email.Split('@')[0]);
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "user";

        var candidate = baseName;
        var counter = 2;
        while (await _db.Users.AnyAsync(u => u.Username == candidate))
        {
            candidate = $"{baseName}{counter}";
            counter++;
        }

        return candidate;
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

        if (request.FirstName != null)
        {
            user.FirstName = request.FirstName.Trim();
            changes.Add("prénom");
        }

        if (request.LastName != null)
        {
            user.LastName = request.LastName.Trim();
            changes.Add("nom");
        }

        if (request.JobTitle != null)
        {
            user.JobTitle = request.JobTitle.Trim();
            changes.Add("fonction");
        }

        if (request.ClearDepartment == true)
        {
            var memberships = await _db.DepartmentMembers
                .Where(m => m.UserId == id)
                .ToListAsync();
            _db.DepartmentMembers.RemoveRange(memberships);
            user.DepartmentId = null;
            changes.Add("département (retiré)");
        }
        else if (request.DepartmentId != null)
        {
            var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == request.DepartmentId.Value);
            if (department == null || department.OrganizationId != user.OrganizationId || !department.IsActive)
                return BadRequest(new { error = "Département introuvable dans cette organisation." });

            var existing = await _db.DepartmentMembers
                .Where(m => m.UserId == id && m.DepartmentId == request.DepartmentId.Value)
                .ToListAsync();
            var others = await _db.DepartmentMembers
                .Where(m => m.UserId == id && m.DepartmentId != request.DepartmentId.Value)
                .ToListAsync();
            foreach (var other in others)
                other.IsPrimary = false;

            if (existing.Count == 0)
            {
                _db.DepartmentMembers.Add(new DepartmentMember
                {
                    DepartmentId = request.DepartmentId.Value,
                    UserId = id,
                    IsPrimary = true,
                    JoinedAt = DateTime.UtcNow,
                });
            }
            else
            {
                foreach (var membership in existing)
                    membership.IsPrimary = true;
            }

            user.DepartmentId = request.DepartmentId.Value;
            changes.Add("département");
        }

        if ((request.FirstName != null || request.LastName != null) &&
            string.IsNullOrWhiteSpace(request.DisplayName))
        {
            user.DisplayName = $"{user.FirstName} {user.LastName}".Trim();
        }

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(currentUserId, AuditActions.AccountUpdated, "User",
            user.Id.ToString(), changes.Count > 0 ? $"Modifications : {string.Join(", ", changes)}" : null,
            "Success", GetClientInfo().Ip, GetClientInfo().UserAgent);

        var loaded = await _userRoleService.GetUserWithRolesAsync(user.Id);
        await _db.Entry(loaded).Reference(u => u.Department).LoadAsync();
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

    [HttpPost("{id}/suspend")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> SuspendUser(int id, [FromBody] SuspendUserRequest request)
    {
        if (id == GetCurrentUserId())
            return BadRequest(new { error = "Vous ne pouvez pas suspendre votre propre compte." });

        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        if (!User.BelongsToOrganization(user.OrganizationId))
            return NotFound(new { error = "Utilisateur introuvable." });

        var now = DateTime.UtcNow;
        var until = request.SuspendUntil ?? now.AddDays(request.DurationDays ?? 7);
        if (until <= now)
            return BadRequest(new { error = "La date de fin de suspension doit être dans le futur." });

        user.Status = AccountStatus.Suspended;
        user.SuspendedUntil = until;
        user.SuspensionReason = request.Reason?.Trim();
        user.IsActive = true;
        await _db.SaveChangesAsync();
        await _db.RevokeAllSessionsAsync(id);

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.AccountSuspended, "User",
            user.Id.ToString(), $"Suspendu jusqu'au {until:yyyy-MM-dd HH:mm}. {request.Reason}", "Success", ip, ua);

        return NoContent();
    }

    [HttpPost("{id}/unsuspend")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> UnsuspendUser(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        user.Status = AccountStatus.Active;
        user.SuspendedUntil = null;
        user.SuspensionReason = null;
        user.IsActive = true;
        await _db.SaveChangesAsync();

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.AccountUnsuspended, "User",
            user.Id.ToString(), "Suspension levée", "Success", ip, ua);

        return NoContent();
    }

    [HttpPost("{id}/reset-password")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> CreateResetInvitation(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        if (!User.BelongsToOrganization(user.OrganizationId))
            return NotFound(new { error = "Utilisateur introuvable." });

        var token = await _invitationService.CreateInvitationAsync(
            id, InvitationType.PasswordReset, GetCurrentUserId());
        var expiresAt = DateTime.UtcNow.AddHours(_invitationService.ExpirationHours);
        user.MustChangePassword = true;
        await _db.SaveChangesAsync();
        await _db.RevokeAllSessionsAsync(id);

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.PasswordReset, "User",
            user.Id.ToString(), "Jeton de réinitialisation émis (lien copiable)", "Success", ip, ua);

        return Ok(new ResetPasswordResultDto
        {
            ResetToken = token,
            ResetLink = $"/api/auth/activate?token={token}",
            ExpiresAt = expiresAt,
        });
    }

    [HttpPost("{id}/resend-activation")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> ResendActivation(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        if (!user.MustChangePassword || user.ActivatedAt != null)
            return BadRequest(new { error = "Ce compte est déjà activé." });

        var token = await _invitationService.CreateInvitationAsync(
            id, InvitationType.AccountActivation, GetCurrentUserId());

        var loaded = await _userRoleService.GetUserWithRolesAsync(id);
        var dto = MapUser(loaded);
        var result = new CreatedUserDto
        {
            Id = dto.Id,
            Username = dto.Username,
            Email = dto.Email,
            DisplayName = dto.DisplayName,
            ActivationToken = token,
            ActivationLink = $"/api/auth/activate?token={token}",
            InvitationExpiresAt = DateTime.UtcNow.AddHours(_invitationService.ExpirationHours),
        };

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.AccountCreated, "User",
            user.Id.ToString(), "Invitation d'activation réémise", "Success", ip, ua);

        return Ok(result);
    }

    [HttpGet("me/sessions")]
    public async Task<IActionResult> GetMySessions()
    {
        var userId = GetCurrentUserId();
        var now = DateTime.UtcNow;
        var sessions = await _db.Sessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .ToListAsync();

        return Ok(sessions.Select(s => MapSession(s, now)));
    }

    [HttpDelete("me/sessions/{sessionId:int}")]
    public async Task<IActionResult> RevokeMySession(int sessionId)
    {
        var session = await _db.Sessions.FirstOrDefaultAsync(
            s => s.Id == sessionId && s.UserId == GetCurrentUserId());
        if (session == null)
            return NotFound(new { error = "Session introuvable." });

        session.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.SessionRevoked, "Session",
            sessionId.ToString(), "Session révoquée par l'utilisateur", "Success", ip, ua);

        return NoContent();
    }

    [HttpGet("{id}/sessions")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> GetUserSessions(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        if (!User.BelongsToOrganization(user.OrganizationId))
            return NotFound(new { error = "Utilisateur introuvable." });

        var now = DateTime.UtcNow;
        var sessions = await _db.Sessions
            .AsNoTracking()
            .Where(s => s.UserId == id)
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .ToListAsync();

        return Ok(sessions.Select(s => MapSession(s, now)));
    }

    [HttpDelete("{id}/sessions/{sessionId:int}")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> RevokeUserSession(int id, int sessionId)
    {
        var session = await _db.Sessions.FirstOrDefaultAsync(
            s => s.Id == sessionId && s.UserId == id);
        if (session == null)
            return NotFound(new { error = "Session introuvable." });

        session.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.SessionRevoked, "Session",
            sessionId.ToString(), $"Session de l'utilisateur {id} révoquée", "Success", ip, ua);

        return NoContent();
    }

    [HttpDelete("{id}/sessions")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> RevokeAllUserSessions(int id)
    {
        var revoked = await _db.RevokeAllSessionsAsync(id);
        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.SessionRevoked, "User",
            id.ToString(), $"{revoked} session(s) révoquée(s)", "Success",
            GetClientInfo().Ip, GetClientInfo().UserAgent);
        return NoContent();
    }

    [HttpGet("me/activity")]
    public async Task<IActionResult> GetMyActivity(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var userId = GetCurrentUserId();
        var size = Math.Clamp(pageSize, 1, 200);
        var activities = await _db.UserActivities
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * size)
            .Take(size)
            .ToListAsync();

        return Ok(activities.Select(MapActivity));
    }

    [HttpGet("{id}/activity")]
    [Authorize(Roles = "SuperAdmin,OrgAdmin,Supervisor")]
    public async Task<IActionResult> GetUserActivity(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        if (!User.IsSuperAdmin() && user.OrganizationId != User.GetUserOrganizationId())
            return NotFound(new { error = "Utilisateur introuvable." });

        var size = Math.Clamp(pageSize, 1, 200);
        var activities = await _db.UserActivities
            .AsNoTracking()
            .Where(a => a.UserId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * size)
            .Take(size)
            .ToListAsync();

        return Ok(activities.Select(MapActivity));
    }

    [HttpPost("import")]
    [Authorize(Roles = AdminRoles)]
    public async Task<IActionResult> ImportUsers(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Fichier CSV attendu dans le champ 'file'." });

        var admin = await _userRoleService.GetUserWithRolesAsync(GetCurrentUserId());
        if (admin == null)
            return Unauthorized();

        var csvText = await ReadFileTextAsync(file);
        var lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 1)
            return BadRequest(new { error = "Le fichier est vide." });

        var result = new UserImportResultDto { Total = Math.Max(0, lines.Length - 1) };
        var separator = lines[0].Contains(';') ? ';' : ',';
        var headers = SplitCsvLine(lines[0], separator);

        var firstNameIndex = IndexOfHeader(headers, "FirstName", "Prénom", "Prenom");
        var lastNameIndex = IndexOfHeader(headers, "LastName", "Nom");
        var emailIndex = IndexOfHeader(headers, "Email", "E-mail");
        var jobTitleIndex = IndexOfHeader(headers, "JobTitle", "Fonction");
        var departmentIndex = IndexOfHeader(headers, "DepartmentId", "Departement", "Département");
        var roleIndex = IndexOfHeader(headers, "Role", "Rôle");

        if (emailIndex < 0)
            return BadRequest(new { error = "Colonne obligatoire manquante : Email." });

        for (var row = 1; row < lines.Length; row++)
        {
            var cells = SplitCsvLine(lines[row], separator);
            var rowResult = new UserImportRowResult { RowNumber = row + 1, Created = false };

            string Cell(int index) => index >= 0 && index < cells.Count ? cells[index].Trim() : string.Empty;

            var rowEmail = Cell(emailIndex);
            rowResult.Email = rowEmail;

            try
            {
                if (string.IsNullOrWhiteSpace(rowEmail) || !rowEmail.Contains('@'))
                    throw new InvalidOperationException("E-mail invalide.");

                if (await _db.Users.AnyAsync(u => u.Email == rowEmail))
                    throw new InvalidOperationException("Cet e-mail est déjà utilisé.");

                var rowFirstName = Cell(firstNameIndex);
                var rowLastName = Cell(lastNameIndex);
                var rowRole = string.IsNullOrWhiteSpace(Cell(roleIndex)) ? RoleKeys.User : Cell(roleIndex);
                if (!RoleKeys.IsValid(rowRole))
                    throw new InvalidOperationException("Rôle invalide.");

                if (!CanAssignRole(rowRole))
                    throw new InvalidOperationException("Vous ne pouvez pas créer ce rôle.");

                int? rowDepartmentId = null;
                if (departmentIndex >= 0 && int.TryParse(Cell(departmentIndex), out var deptId))
                {
                    var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == deptId);
                    if (department == null || department.OrganizationId != admin.OrganizationId)
                        throw new InvalidOperationException("Département introuvable.");
                    rowDepartmentId = deptId;
                }

                var username = await GenerateUniqueUsernameAsync(rowFirstName, rowLastName, rowEmail);
                var user = new User
                {
                    Username = username,
                    Email = rowEmail,
                    DisplayName = $"{rowFirstName} {rowLastName}".Trim(),
                    FirstName = rowFirstName,
                    LastName = rowLastName,
                    JobTitle = Cell(jobTitleIndex),
                    DepartmentId = rowDepartmentId,
                    PasswordHash = _passwordHasher.Hash(Guid.NewGuid().ToString("N")),
                    IsActive = true,
                    Status = AccountStatus.Active,
                    OrganizationId = admin.OrganizationId,
                    MustChangePassword = true,
                    CreatedAt = DateTime.UtcNow,
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                await _userRoleService.AssignDefaultRoleAsync(user);
                if (rowRole != RoleKeys.User)
                    await _userRoleService.SetOrganizationRoleAsync(user, rowRole);
                else
                    await _db.SaveChangesAsync();

                var token = await _invitationService.CreateInvitationAsync(
                    user.Id, InvitationType.AccountActivation, admin.Id);

                result.Created++;
                rowResult.Created = true;
                result.Activations.Add(new ImportedActivationDto
                {
                    UserId = user.Id,
                    Email = rowEmail,
                    Username = username,
                    ActivationToken = token,
                    ActivationLink = $"/api/auth/activate?token={token}",
                });
            }
            catch (Exception ex)
            {
                result.Skipped++;
                rowResult.Error = ex.Message;
            }

            result.Rows.Add(rowResult);
        }

        var (ip, ua) = GetClientInfo();
        await _auditService.LogAsync(admin.Id, AuditActions.AccountCreated, "User", "import",
            $"Import CSV : {result.Created} créés, {result.Skipped} ignorés", "Success", ip, ua);

        return Ok(result);
    }

    private static async Task<string> ReadFileTextAsync(IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        return await reader.ReadToEndAsync();
    }

    private static List<string> SplitCsvLine(string line, char separator)
    {
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (ch == separator && !inQuotes)
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }
            current.Append(ch);
        }
        cells.Add(current.ToString().Trim());
        return cells;
    }

    private static int IndexOfHeader(List<string> headers, params string[] names) =>
        headers.FindIndex(h => names.Any(n => string.Equals(
            h.Trim().Trim('"'), n, StringComparison.OrdinalIgnoreCase)));

    private static SessionDto MapSession(Session session, DateTime now) => new()
    {
        Id = session.Id,
        TokenId = session.TokenId,
        IpAddress = session.IpAddress,
        UserAgent = session.UserAgent,
        ExpiresAt = session.ExpiresAt,
        RevokedAt = session.RevokedAt,
        LastSeenAt = session.LastSeenAt,
        CreatedAt = session.CreatedAt,
        IsActive = session.RevokedAt == null && session.ExpiresAt >= now,
    };

    private static ActivityItemDto MapActivity(UserActivity activity) => new()
    {
        Id = activity.Id,
        Type = activity.Type,
        Details = activity.Details,
        IpAddress = activity.IpAddress,
        UserAgent = activity.UserAgent,
        CreatedAt = activity.CreatedAt,
    };

    private static UserDto MapUser(User user)
    {
        var membership = user.DepartmentMembers
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => m.DepartmentId)
            .FirstOrDefault();

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            JobTitle = user.JobTitle,
            DepartmentId = user.DepartmentId ?? membership?.DepartmentId,
            DepartmentName = user.Department?.Name ?? membership?.Department?.Name,
            OrganizationId = user.OrganizationId,
            Role = user.UserRoles.Count == 0
                ? RoleKeys.User
                : RoleKeys.HighestRole(user.UserRoles.Select(ur => ur.Role.Key)),
            IsActive = user.IsActive,
            Status = user.Status.ToString(),
            SuspendedUntil = user.SuspendedUntil,
            SuspensionReason = user.SuspensionReason,
            MustChangePassword = user.MustChangePassword,
            ActivatedAt = user.ActivatedAt,
            LastLoginAt = user.LastLoginAt,
            IsOnline = ChatHub.OnlineUserIds().Contains(user.Id),
        };
    }
}
