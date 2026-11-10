using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Entities;
using OrgChat.Core.Enums;
using OrgChat.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;

namespace OrgChat.Infrastructure.Services;

/// <summary>
/// Génération et vérification des jetons d'activation.
/// Format du jeton : base64url(32 octets aléatoires). Stockage : SHA-256 hex.
/// Expiration configurée par OrgChat:InvitationExpirationHours (défaut 72h),
/// usage unique (UsedAt) et révocable à tout moment.
/// </summary>
public class InvitationService : IInvitationService
{
    private readonly AppDbContext _db;
    private readonly int _expirationHours;

    public InvitationService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _expirationHours = int.TryParse(configuration["OrgChat:InvitationExpirationHours"], out var hours) && hours > 0
            ? hours
            : 72;
    }

    public int ExpirationHours => _expirationHours;

    public async Task<string> CreateInvitationAsync(
        int userId,
        InvitationType type,
        int createdByUserId)
    {
        // Une seule invitation active par utilisateur et par type.
        await RevokePendingAsync(userId, type);

        var plainToken = GenerateSecureToken();
        var tokenHash = HashToken(plainToken);

        var invitation = new Invitation
        {
            UserId = userId,
            Type = type,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddHours(_expirationHours),
            CreatedByUserId = createdByUserId,
        };

        _db.Invitations.Add(invitation);
        await _db.SaveChangesAsync();

        return plainToken;
    }

    public async Task<Invitation?> ValidateAndConsumeAsync(string plainToken, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(plainToken))
            return null;

        var tokenHash = HashToken(plainToken.Trim());
        var invitation = await _db.Invitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash);
        if (invitation == null)
            return null;

        var now = DateTime.UtcNow;
        if (invitation.UsedAt != null || invitation.ExpiresAt <= now)
            return null;

        invitation.UsedAt = now;
        invitation.UsedByIp = ipAddress;
        await _db.SaveChangesAsync();
        return invitation;
    }

    public async Task RevokePendingAsync(int userId, InvitationType? type = null)
    {
        var now = DateTime.UtcNow;
        var pending = await _db.Invitations
            .Where(i => i.UserId == userId && i.UsedAt == null && i.ExpiresAt > now &&
                        (type == null || i.Type == type))
            .ToListAsync();

        foreach (var invitation in pending)
        {
            invitation.UsedAt = now;
            invitation.UsedByIp = "révoquée";
        }

        if (pending.Count > 0)
            await _db.SaveChangesAsync();
    }

    public static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string HashToken(string plainToken)
    {
        var bytes = Encoding.UTF8.GetBytes(plainToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
