using System.Collections.Generic;

namespace OrgChat.Core.Entities;

/// <summary>
/// Rôle système du RBAC. Les rôles sont sémés (Id 1 à 5) et référencés par
/// la jointure des rôles d'utilisateur ; la table permet à terme
/// une configuration fine des rôles sans changer le code.
/// </summary>
public class Role
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; } = true;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
