using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Constants;
using OrgChat.Core.Contracts.Departments;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Entities;
using OrgChat.Infrastructure.Data;
using OrgChat.Server.Hubs;
using System.Security.Claims;

namespace OrgChat.Server.Controllers;

[ApiController]
[Route("api/departments")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _auditService;

    public DepartmentsController(AppDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdClaim != null ? int.Parse(userIdClaim) : 0;
    }

    /// <summary>
    /// Le département demandé existe-t-il dans le périmètre de l'utilisateur ?
    /// Bloque la lecture croisée entre organisations (isolation stricte).
    /// </summary>
    private async Task<Department?> GetDepartmentInScopeAsync(int id)
    {
        var department = await _db.Departments
            .Include(d => d.Organization)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (department == null || !User.BelongsToOrganization(department.OrganizationId))
            return null;

        return department;
    }

    /// <summary>Pour la gestion des membres : OrgAdmin/SuperAdmin du département,
    /// ou Responsable de département désigné.</summary>
    private bool CanManageMembers(Department department) =>
        User.IsOrgAdminHere() || (User.IsDepartmentManager() && department.ManagerUserId == GetCurrentUserId());

    [HttpGet]
    public async Task<IActionResult> GetDepartments()
    {
        var departments = await _db.Departments
            .AsNoTracking()
            .Include(d => d.Manager)
            .Include(d => d.Members)
            .Where(d => User.IsSuperAdmin() || d.OrganizationId == User.GetUserOrganizationId())
            .OrderBy(d => d.Name)
            .ToListAsync();

        return Ok(departments.Select(d => new DepartmentDto
        {
            Id = d.Id,
            Name = d.Name,
            Description = d.Description,
            IsActive = d.IsActive,
            OrganizationId = d.OrganizationId,
            ManagerUserId = d.ManagerUserId,
            ManagerName = d.Manager?.DisplayName,
            MemberCount = d.Members.Count,
            CreatedAt = d.CreatedAt,
        }));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDepartment(int id)
    {
        var department = await _db.Departments
            .Include(d => d.Manager)
            .Include(d => d.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (department == null || !User.BelongsToOrganization(department.OrganizationId))
            return NotFound(new { error = "Département introuvable." });

        var organization = await _db.Organizations.AsNoTracking()
            .FirstAsync(o => o.Id == department.OrganizationId);

        var dto = new DepartmentDto
        {
            Id = department.Id,
            Name = department.Name,
            Description = department.Description,
            IsActive = department.IsActive,
            OrganizationId = department.OrganizationId,
            ManagerUserId = department.ManagerUserId,
            ManagerName = department.Manager?.DisplayName,
            MemberCount = department.Members.Count,
            CreatedAt = department.CreatedAt,
        };

        var response = new
        {
            department = dto,
            members = department.Members
                .OrderByDescending(m => m.IsPrimary)
                .ThenBy(m => m.User.DisplayName)
                .Select(m => new DepartmentMemberDto
                {
                    UserId = m.UserId,
                    Username = m.User.Username,
                    DisplayName = m.User.DisplayName,
                    JobTitle = m.User.JobTitle,
                    IsPrimary = m.IsPrimary,
                    JoinedAt = m.JoinedAt,
                }),
        };

        return Ok(response);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,OrgAdmin")]
    public async Task<IActionResult> Create([FromBody] DepartmentCreateRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var organizationId = User.GetUserOrganizationId();
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { error = "Le nom du département est requis." });

        if (await _db.Departments.AnyAsync(d => d.OrganizationId == organizationId && d.Name == name))
            return BadRequest(new { error = "Un département portant ce nom existe déjà." });

        var department = new Department
        {
            OrganizationId = organizationId,
            Name = name,
            Description = request.Description?.Trim(),
            IsActive = true,
        };

        if (request.ManagerUserId != null)
        {
            var manager = await _db.Users.FindAsync(request.ManagerUserId.Value);
            if (manager == null || !User.BelongsToOrganization(manager.OrganizationId))
                return BadRequest(new { error = "Le responsable désigné doit appartenir à votre organisation." });
            department.ManagerUserId = manager.Id;
        }

        _db.Departments.Add(department);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.DepartmentCreated,
            "Department", department.Id.ToString(),
            JayRequestDescription(department, request.Description),
            "Success", Ip(), UserAgent());

        return CreatedAtAction(nameof(GetDepartment), new { id = department.Id }, MapDepartment(department));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "SuperAdmin,OrgAdmin")]
    public async Task<IActionResult> Update(int id, [FromBody] DepartmentUpdateRequest request)
    {
        var department = await GetDepartmentInScopeAsync(id);
        if (department == null)
            return NotFound(new { error = "Département introuvable." });

        var changes = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Name) &&
            !string.Equals(request.Name.Trim(), department.Name, StringComparison.Ordinal))
        {
            var newName = request.Name.Trim();
            if (await _db.Departments.AnyAsync(d =>
                    d.OrganizationId == department.OrganizationId && d.Name == newName && d.Id != id))
                return BadRequest(new { error = "Un département portant ce nom existe déjà." });
            department.Name = newName;
            changes.Add("nom");
        }

        if (request.Description != null && request.Description.Trim() != department.Description)
        {
            department.Description = request.Description.Trim();
            changes.Add("description");
        }

        if (request.ManagerUserId != department.ManagerUserId)
        {
            if (request.ManagerUserId == null)
            {
                department.ManagerUserId = null;
                changes.Add("responsable retiré");
            }
            else
            {
                var manager = await _db.Users.FindAsync(request.ManagerUserId.Value);
                if (manager == null || !User.BelongsToOrganization(manager.OrganizationId))
                    return BadRequest(new { error = "Le responsable désigné doit appartenir à votre organisation." });
                department.ManagerUserId = manager.Id;
                changes.Add("responsable");
            }
        }

        if (request.IsActive != null && request.IsActive != department.IsActive)
        {
            department.IsActive = request.IsActive.Value;
            changes.Add(department.IsActive ? "réactivation" : "désactivation");
        }

        if (changes.Count > 0)
            await _db.SaveChangesAsync();

        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.DepartmentUpdated,
            "Department", department.Id.ToString(),
            changes.Count > 0 ? $"Modifications : {string.Join(", ", changes)}" : "Aucun changement",
            "Success", Ip(), UserAgent());

        return Ok(new DepartmentDto
        {
            Id = department.Id,
            Name = department.Name,
            Description = department.Description,
            IsActive = department.IsActive,
            OrganizationId = department.OrganizationId,
            ManagerUserId = department.ManagerUserId,
            MemberCount = await _db.DepartmentMembers.CountAsync(m => m.DepartmentId == department.Id),
            CreatedAt = department.CreatedAt,
        });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "SuperAdmin,OrgAdmin")]
    public async Task<IActionResult> Delete(int id)
    {
        var department = await GetDepartmentInScopeAsync(id);
        if (department == null)
            return NotFound(new { error = "Département introuvable." });

        var hasMembers = await _db.DepartmentMembers.AnyAsync(m => m.DepartmentId == id);
        if (hasMembers)
            return BadRequest(new { error = "Retirez d'abord les membres du département avant de le supprimer." });

        // Les utilisateurs rattachés par le raccourci DepartmentId perdent leur département.
        var linkedUsers = await _db.Users.Where(u => u.DepartmentId == id).ToListAsync();
        foreach (var linkedUser in linkedUsers)
            linkedUser.DepartmentId = null;

        _db.Departments.Remove(department);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.DepartmentUpdated,
            "Department", id.ToString(), $"Département « {department.Name} » supprimé",
            "Success", Ip(), UserAgent());

        return NoContent();
    }

    /// <summary>Affecte un utilisateur au département (OrgAdmin/SuperAdmin ou Responsable désigné).</summary>
    [HttpPost("{id:int}/members")]
    public async Task<IActionResult> AddMember(int id, [FromBody] AddDepartmentMemberRequest request)
    {
        var department = await GetDepartmentInScopeAsync(id);
        if (department == null)
            return NotFound(new { error = "Département introuvable." });

        if (!CanManageMembers(department))
            return Forbid();

        var user = await _db.Users.FindAsync(request.UserId);
        if (user == null || user.OrganizationId != department.OrganizationId)
            return BadRequest(new { error = "Utilisateur introuvable dans cette organisation." });

        if (await _db.DepartmentMembers.AnyAsync(m => m.DepartmentId == id && m.UserId == request.UserId))
            return BadRequest(new { error = "Cet utilisateur est déjà affecté à ce département." });

        if (request.IsPrimary)
        {
            var previousPrimaries = await _db.DepartmentMembers
                .Where(m => m.UserId == request.UserId && m.IsPrimary)
                .ToListAsync();
            foreach (var previous in previousPrimaries)
                previous.IsPrimary = false;

            user.DepartmentId = id;
        }

        _db.DepartmentMembers.Add(new DepartmentMember
        {
            DepartmentId = id,
            UserId = request.UserId,
            IsPrimary = request.IsPrimary,
            JoinedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.DepartmentMemberAdded,
            "DepartmentMember", $"{request.UserId}→{id}",
            $"{user.DisplayName} affecté à « {department.Name} »",
            "Success", Ip(), UserAgent());

        return NoContent();
    }

    [HttpDelete("{id:int}/members/{userId:int}")]
    public async Task<IActionResult> RemoveMember(int id, int userId)
    {
        var department = await GetDepartmentInScopeAsync(id);
        if (department == null)
            return NotFound(new { error = "Département introuvable." });

        if (!CanManageMembers(department))
            return Forbid();

        var assignment = await _db.DepartmentMembers.FirstOrDefaultAsync(m => m.DepartmentId == id && m.UserId == userId);
        if (assignment == null)
            return NotFound(new { error = "Cet utilisateur n'est pas affecté à ce département." });

        var willRemovePrimary = assignment.IsPrimary;
        _db.DepartmentMembers.Remove(assignment);

        if (willRemovePrimary)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user != null && user.DepartmentId == id)
            {
                // Le département principal devient la première autre affectation.
                var nextPrimary = await _db.DepartmentMembers
                    .Where(m => m.UserId == userId && m.DepartmentId != id)
                    .OrderBy(m => m.Id)
                    .FirstOrDefaultAsync();
                user.DepartmentId = nextPrimary?.DepartmentId;
            }
        }

        await _db.SaveChangesAsync();

        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.DepartmentMemberRemoved,
            "DepartmentMember", $"{userId}→{id}",
            $"Affectation retirée pour « {department.Name} »",
            "Success", Ip(), UserAgent());

        return NoContent();
    }

    /// <summary>Statistiques d'un département (métadonnées, jamais le contenu des conversations).</summary>
    [HttpGet("{id:int}/stats")]
    public async Task<IActionResult> GetStats(int id)
    {
        var department = await GetDepartmentInScopeAsync(id);
        if (department == null)
            return NotFound(new { error = "Département introuvable." });

        var memberUserIds = await _db.DepartmentMembers
            .Where(m => m.DepartmentId == id)
            .Select(m => m.UserId)
            .ToListAsync();

        var onlineUserIds = ChatHub.OnlineUserIds().ToHashSet();

        var activeUsers = await _db.Users.CountAsync(u =>
            memberUserIds.Contains(u.Id) && u.IsActive &&
            (u.Status != OrgChat.Core.Enums.AccountStatus.Disabled));

        var messageCount = await _db.Messages
            .Where(m => !m.IsDeleted && memberUserIds.Contains(m.SenderId))
            .CountAsync();

        var conversationCount = await _db.ConversationParticipants
            .Where(p => memberUserIds.Contains(p.UserId))
            .Select(p => p.ConversationId)
            .Distinct()
            .CountAsync();

        return Ok(new DepartmentStatsDto
        {
            DepartmentId = id,
            MemberCount = memberUserIds.Count,
            ActiveUsers = activeUsers,
            OnlineUsers = memberUserIds.Count(onlineUserIds.Contains),
            MessageCount = messageCount,
            ConversationCount = conversationCount,
        });
    }

    private static string JayRequestDescription(Department department, string? description) =>
        $"Département « {department.Name} »" + (string.IsNullOrWhiteSpace(description) ? "" : $" — {description.Trim()}");

    private static DepartmentDto MapDepartment(Department department) => new()
    {
        Id = department.Id,
        Name = department.Name,
        Description = department.Description,
        IsActive = department.IsActive,
        OrganizationId = department.OrganizationId,
        ManagerUserId = department.ManagerUserId,
        CreatedAt = department.CreatedAt,
    };

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? UserAgent() => Request.Headers.UserAgent.ToString();
}
