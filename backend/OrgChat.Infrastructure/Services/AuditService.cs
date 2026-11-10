using OrgChat.Core.Constants;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Entities;
using OrgChat.Infrastructure.Data;

namespace OrgChat.Infrastructure.Services;

/// <summary>
/// Journalisation d'audit et d'activité, en écriture seule. Aucune méthode
/// de modification ni de suppression : l'intégrité du journal est protégée
/// au niveau service et renforcée par des triggers SQLite (migrations).
/// </summary>
public class AuditService : IAuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(
        int? actorUserId,
        string action,
        string entityType = "",
        string? entityId = null,
        string? details = null,
        string result = "Success",
        string? ipAddress = null,
        string? userAgent = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            Result = result,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
    }

    public async Task LogUserActivityAsync(
        int userId,
        string type,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        _db.UserActivities.Add(new UserActivity
        {
            UserId = userId,
            Type = type,
            Details = details,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
    }
}
