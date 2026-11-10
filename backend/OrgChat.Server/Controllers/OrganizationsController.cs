using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Constants;
using OrgChat.Core.Contracts.Organizations;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Entities;
using OrgChat.Infrastructure.Data;
using System.Security.Claims;

namespace OrgChat.Server.Controllers;

[ApiController]
[Route("api/organizations")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private static readonly HashSet<string> AllowedLogoExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico" };
    private const long MaxLogoBytes = 2 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly IAuditService _auditService;
    private readonly ILogger<OrganizationsController> _logger;

    public OrganizationsController(
        AppDbContext db,
        IAuditService auditService,
        ILogger<OrganizationsController> logger)
    {
        _db = db;
        _auditService = auditService;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdClaim != null ? int.Parse(userIdClaim) : 0;
    }

    private int GetOrganizationId() => User.GetUserOrganizationId();

    /// <summary>L'organisation courante de l'utilisateur (scope).</summary>
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent()
    {
        var organizationId = GetOrganizationId();
        if (organizationId == 0)
            return NotFound(new { error = "Aucune organisation associée à ce compte." });

        var organization = await GetOrganizationDtoAsync(organizationId);
        if (organization == null)
            return NotFound(new { error = "Organisation introuvable." });

        return Ok(organization);
    }

    /// <summary>Toutes les organisations (SuperAdmin uniquement).</summary>
    [HttpGet]
    [Authorize(Roles = RoleKeys.SuperAdmin)]
    public async Task<IActionResult> GetAll()
    {
        var organizations = await _db.Organizations
            .AsNoTracking()
            .OrderBy(o => o.Name)
            .ToListAsync();

        var items = new List<OrganizationDto>(organizations.Count);
        foreach (var organization in organizations)
            items.Add(await GetOrganizationDtoAsync(organization.Id) ?? MapBasic(organization));

        return Ok(items);
    }

    [HttpPost]
    [Authorize(Roles = RoleKeys.SuperAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { error = "Le nom de l'organisation est requis." });

        if (await _db.Organizations.AnyAsync(o => o.Name == name))
            return BadRequest(new { error = "Ce nom d'organisation est déjà utilisé." });

        var organization = new Organization
        {
            Name = name,
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            Address = request.Address?.Trim(),
            IsActive = true,
        };

        _db.Organizations.Add(organization);
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.OrganizationCreated,
            "Organization", organization.Id.ToString(), $"Nom : {organization.Name}",
            "Success", Ip(), UserAgent());

        return CreatedAtAction(nameof(GetCurrent), MapBasic(await _db.Organizations.FindAsync(organization.Id)));
    }

    /// <summary>Modification de l'organisation courante (SuperAdmin ou OrgAdmin).</summary>
    [HttpPut("current")]
    [Authorize(Roles = "SuperAdmin,OrgAdmin")]
    public async Task<IActionResult> UpdateCurrent([FromBody] UpdateOrganizationRequest request)
    {
        var organizationId = GetOrganizationId();
        if (organizationId == 0 && !User.IsSuperAdmin())
            return NotFound(new { error = "Aucune organisation associée à ce compte." });

        // Un SuperAdmin sans organisation est autorisé à éditer l'organisation 1.
        var targetId = organizationId != 0 ? organizationId : 1;
        var organization = await _db.Organizations.FindAsync(targetId);
        if (organization == null)
            return NotFound(new { error = "Organisation introuvable." });

        var changes = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Name) &&
            !string.Equals(request.Name.Trim(), organization.Name, StringComparison.Ordinal))
        {
            var newName = request.Name.Trim();
            if (await _db.Organizations.AnyAsync(o => o.Name == newName && o.Id != targetId))
                return BadRequest(new { error = "Ce nom d'organisation est déjà utilisé." });
            organization.Name = newName;
            changes.Add("nom");
        }

        if (request.Email != null && request.Email.Trim() != organization.Email)
        {
            organization.Email = request.Email.Trim();
            changes.Add("email");
        }
        if (request.Phone != null && request.Phone.Trim() != organization.Phone)
        {
            organization.Phone = request.Phone.Trim();
            changes.Add("téléphone");
        }
        if (request.Address != null && request.Address.Trim() != organization.Address)
        {
            organization.Address = request.Address.Trim();
            changes.Add("adresse");
        }

        if (changes.Count > 0)
            await _db.SaveChangesAsync();

        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.OrganizationUpdated,
            "Organization", organization.Id.ToString(),
            changes.Count > 0 ? $"Modifications : {string.Join(", ", changes)}" : "Aucun changement",
            "Success", Ip(), UserAgent());

        return Ok(MapBasic(organization));
    }

    [HttpPut("{id:int}/activate")]
    [Authorize(Roles = RoleKeys.SuperAdmin)]
    public async Task<IActionResult> Activate(int id)
    {
        var organization = await _db.Organizations.FindAsync(id);
        if (organization == null)
            return NotFound(new { error = "Organisation introuvable." });

        organization.IsActive = true;
        await _db.SaveChangesAsync();
        await AuditOrgStatusAsync(organization, true);
        return NoContent();
    }

    [HttpPut("{id:int}/deactivate")]
    [Authorize(Roles = RoleKeys.SuperAdmin)]
    public async Task<IActionResult> Deactivate(int id)
    {
        var organization = await _db.Organizations.FindAsync(id);
        if (organization == null)
            return NotFound(new { error = "Organisation introuvable." });

        if (OrganizationProtected(organization.Id) && organization.Id == 1)
            return BadRequest(new { error = "Impossible de désactiver l'organisation par défaut." });

        organization.IsActive = false;
        await _db.SaveChangesAsync();
        await AuditOrgStatusAsync(organization, false);
        return NoContent();
    }

    /// <summary>Envoi du logo de l'organisation courante (multipart 'file').</summary>
    [HttpPut("current/logo")]
    [Authorize(Roles = "SuperAdmin,OrgAdmin")]
    public async Task<IActionResult> UploadLogo(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Logo attendu dans le champ 'file'." });

        if (file.Length > MaxLogoBytes)
            return BadRequest(new { error = "Le logo doit faire au plus 2 Mo." });

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedLogoExtensions.Contains(extension))
            return BadRequest(new { error = "Extension de logo non supportée (.png, .jpg, .jpeg, .gif, .webp, .ico)." });

        var organizationId = GetOrganizationId();
        var organization = await _db.Organizations.FindAsync(organizationId);
        if (organization == null)
            return NotFound(new { error = "Organisation introuvable." });

        var uploadsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "logos");
        Directory.CreateDirectory(uploadsDirectory);

        // L'ancien logo est supprimé pour éviter l'accumulation de fichiers.
        if (!string.IsNullOrEmpty(organization.LogoPath) && System.IO.File.Exists(organization.LogoPath))
        {
            try { System.IO.File.Delete(organization.LogoPath); } catch { /* best effort */ }
        }

        var storagePath = Path.Combine(uploadsDirectory, $"org{organizationId}{extension.ToLowerInvariant()}");
        await using (var stream = System.IO.File.Create(storagePath))
        {
            await file.CopyToAsync(stream);
        }

        organization.LogoPath = storagePath;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync(GetCurrentUserId(), AuditActions.OrganizationUpdated,
            "Organization", organization.Id.ToString(), "Logo mis à jour", "Success", Ip(), UserAgent());

        return NoContent();
    }

    [HttpGet("{id:int}/logo")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLogo(int id)
    {
        var organization = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (organization == null || string.IsNullOrEmpty(organization.LogoPath) ||
            !System.IO.File.Exists(organization.LogoPath))
            return NotFound();

        var contentType = Path.GetExtension(organization.LogoPath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream",
        };

        return PhysicalFile(organization.LogoPath, contentType, enableRangeProcessing: false);
    }

    private async Task<OrganizationDto?> GetOrganizationDtoAsync(int organizationId)
    {
        var organization = await _db.Organizations.FindAsync(organizationId);
        if (organization == null)
            return null;

        var userCount = await _db.Users.CountAsync(u => u.OrganizationId == organizationId && u.IsActive);
        var departmentCount = await _db.Departments.CountAsync(d => d.OrganizationId == organizationId && d.IsActive);

        return new OrganizationDto
        {
            Id = organization.Id,
            Name = organization.Name,
            Email = organization.Email,
            Phone = organization.Phone,
            Address = organization.Address,
            IsActive = organization.IsActive,
            UserCount = userCount,
            DepartmentCount = departmentCount,
        };
    }

    private async Task AuditOrgStatusAsync(Organization organization, bool activated) =>
        await _auditService.LogAsync(GetCurrentUserId(),
            activated ? AuditActions.OrganizationUpdated : AuditActions.OrganizationUpdated,
            "Organization", organization.Id.ToString(),
            activated ? "Organisation activée" : "Organisation désactivée",
            "Success", Ip(), UserAgent());

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();

    private string? UserAgent() => Request.Headers.UserAgent.ToString();

    private OrganizationDto MapBasic(Organization organization) => new()
    {
        Id = organization.Id,
        Name = organization.Name,
        Email = organization.Email,
        Phone = organization.Phone,
        Address = organization.Address,
        IsActive = organization.IsActive,
    };

    private static bool OrganizationProtected(int id) => id == 1;
}
