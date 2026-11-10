using OrgChat.Core.Enums;
using System;

namespace OrgChat.Core.Entities;

/// <summary>
/// Jeton sécurisé à usage unique : activation de compte (définition du mot de
/// passe initial) ou réinitialisation de mot de passe. Seul le hash du jeton
/// est stocké ; le jeton en clair n'est jamais persisté.
/// </summary>
public class Invitation : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public InvitationType Type { get; set; }

    /// <summary>Hash SHA-256 hexadécimal (64 caractères) du jeton.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedByIp { get; set; }

    /// <summary>Utilisateur (administrateur) ayant émis l'invitation.</summary>
    public int CreatedByUserId { get; set; }
    public User CreatedBy { get; set; } = null!;
}
