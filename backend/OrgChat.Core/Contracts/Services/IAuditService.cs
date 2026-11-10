using System.Threading.Tasks;

namespace OrgChat.Core.Contracts.Services;

public interface IAuditService
{
    /// <summary>Écrit une entrée du journal d'audit (append-only).</summary>
    Task LogAsync(
        int? actorUserId,
        string action,
        string entityType = "",
        string? entityId = null,
        string? details = null,
        string result = "Success",
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>Trace d'activité utilisateur (métadonnées uniquement).</summary>
    Task LogUserActivityAsync(
        int userId,
        string type,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null);
}
