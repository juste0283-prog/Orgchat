using System;

namespace OrgChat.Core.Entities;

/// <summary>Affectation d'un utilisateur à un département.</summary>
public class DepartmentMember : BaseEntity
{
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Département principal de l'utilisateur ; la conception
    /// permet d'ajouter ultérieurement plusieurs affectations.</summary>
    public bool IsPrimary { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
