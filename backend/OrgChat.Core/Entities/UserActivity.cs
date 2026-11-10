using System;

namespace OrgChat.Core.Entities;

/// <summary>
/// Trace d'activité non intruse d'un utilisateur (connexions, activation...)
/// destinée aux statistiques de supervision. Métadonnées uniquement :
/// jamais le contenu des conversations.
/// </summary>
public class UserActivity : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Type d'activité (voir OrgChat.Core.Constants.ActivityTypes).</summary>
    public string Type { get; set; } = string.Empty;

    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
