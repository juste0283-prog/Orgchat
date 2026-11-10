using System;

namespace OrgChat.Core.Entities;

/// <summary>
/// Journal d'audit, en écriture seule : aucune opération de mise à jour ou de
/// suppression n'existe et un trigger SQLite bloque toute tentative de
/// falsification en base. Voir AuditLogConfiguration.
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>Utilisateur à l'origine de l'action, null pour le système.</summary>
    public int? ActorUserId { get; set; }
    public User? Actor { get; set; }

    /// <summary>Type d'action (voir OrgChat.Core.Constants.AuditActions).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Type d'entité concernée (ex. "User", "Department").</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Identifiant de l'entité concernée.</summary>
    public string? EntityId { get; set; }

    /// <summary>Détails libres (JSON) : ancienne/nouvelle valeur, résultat...</summary>
    public string? Details { get; set; }

    /// <summary>Résultat de l'opération : Success, Denied, Error...</summary>
    public string Result { get; set; } = "Success";

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
