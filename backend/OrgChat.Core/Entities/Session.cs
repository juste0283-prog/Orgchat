using System;

namespace OrgChat.Core.Entities;

/// <summary>
/// Session de connexion persistée : permet de lister les sessions actives,
/// de les révoquer et de vérifier côté serveur, à chaque requête, que la
/// session associée au jeton JWT (claim jti) est toujours valide.
/// </summary>
public class Session : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Identifiant unique du jeton JWT (claim jti).</summary>
    public string TokenId { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
