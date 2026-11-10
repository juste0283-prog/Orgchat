using Microsoft.EntityFrameworkCore;
using OrgChat.Infrastructure.Data;

namespace OrgChat.Infrastructure.Data;

public static class AppDbContextExtensions
{
    /// <summary>Révoque toutes les sessions actives d'un utilisateur (désactivation, sécurité).</summary>
    public static async Task<int> RevokeAllSessionsAsync(
        this AppDbContext db,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var activeSessions = await db.Sessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt >= now)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
            session.RevokedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        return activeSessions.Count;
    }
}
