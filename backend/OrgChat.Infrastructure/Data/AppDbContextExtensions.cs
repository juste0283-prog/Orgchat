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
        return await db.Sessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt >= now)
            .ExecuteUpdateAsync(
                s => s.SetProperty(se => se.RevokedAt, now),
                cancellationToken);
    }
}
