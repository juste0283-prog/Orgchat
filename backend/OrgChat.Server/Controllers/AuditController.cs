using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Contracts.Supervision;
using OrgChat.Core.Entities;
using OrgChat.Infrastructure.Data;

namespace OrgChat.Server.Controllers;

/// <summary>
/// Lecture du journal d'audit (append-only). Réservé aux rôles de supervision.
/// Le journal n'expose que des métadonnées d'action ; il ne contient jamais le
/// contenu des conversations. Un non-SuperAdmin ne voit que les entrées dont
/// l'acteur appartient à son organisation.
/// </summary>
[ApiController]
[Route("api/audit")]
[Authorize(Roles = "SuperAdmin,OrgAdmin,Supervisor")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? search = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? result = null,
        [FromQuery] int? actorUserId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var query = _db.AuditLogs
            .AsNoTracking()
            .Include(a => a.Actor)
            .AsQueryable();

        if (!User.IsSuperAdmin())
        {
            var organizationId = User.GetUserOrganizationId();
            query = query.Where(a => a.Actor != null && a.Actor.OrganizationId == organizationId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(a =>
                a.Action.ToLower().Contains(term) ||
                a.EntityType.ToLower().Contains(term) ||
                (a.EntityId != null && a.EntityId.ToLower().Contains(term)) ||
                (a.Details != null && a.Details.ToLower().Contains(term)) ||
                (a.Actor != null && a.Actor.DisplayName.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);

        if (!string.IsNullOrWhiteSpace(result))
            query = query.Where(a => a.Result == result);

        if (actorUserId is > 0)
            query = query.Where(a => a.ActorUserId == actorUserId);

        if (from != null)
        {
            var fromUtc = from.Value.Kind == DateTimeKind.Utc
                ? from.Value
                : from.Value.ToUniversalTime();
            query = query.Where(a => a.CreatedAt >= fromUtc);
        }

        if (to != null)
        {
            var toUtc = to.Value.Kind == DateTimeKind.Utc
                ? to.Value
                : to.Value.ToUniversalTime();
            query = query.Where(a => a.CreatedAt <= toUtc);
        }

        var total = await query.CountAsync();
        var size = Math.Clamp(pageSize, 1, 100);
        var currentPage = Math.Max(1, page);

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .ToListAsync();

        return Ok(new PagedResultDto<AuditLogDto>
        {
            Items = logs.Select(MapLog).ToList(),
            TotalCount = total,
            Page = currentPage,
            PageSize = size,
        });
    }

    /// <summary>Valeurs distinctes utiles aux filtres de l'interface.</summary>
    [HttpGet("facets")]
    public async Task<IActionResult> GetFacets()
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!User.IsSuperAdmin())
        {
            var organizationId = User.GetUserOrganizationId();
            query = query.Where(a => a.Actor != null && a.Actor.OrganizationId == organizationId);
        }

        var actions = await query.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();
        var entityTypes = await query.Select(a => a.EntityType).Distinct().OrderBy(e => e).ToListAsync();
        var results = await query.Select(a => a.Result).Distinct().OrderBy(r => r).ToListAsync();

        return Ok(new { actions, entityTypes, results });
    }

    private static AuditLogDto MapLog(AuditLog log) => new()
    {
        Id = log.Id,
        CreatedAt = log.CreatedAt,
        ActorUserId = log.ActorUserId,
        ActorName = log.Actor?.DisplayName ?? "Système",
        Action = log.Action,
        EntityType = log.EntityType,
        EntityId = log.EntityId,
        Details = log.Details,
        Result = log.Result,
        IpAddress = log.IpAddress,
    };
}
