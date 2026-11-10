using System;
using System.Collections.Generic;

namespace OrgChat.Core.Entities;

/// <summary>
/// Attribution d'un rôle à un utilisateur. OrganizationId null = rôle global
/// (SuperAdmin uniquement) ; sinon le rôle s'applique à une organisation
/// précise, ce qui prépare le multi-organisations.
/// </summary>
public class UserRole : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
}
