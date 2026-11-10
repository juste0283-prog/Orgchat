using System.Collections.Generic;

namespace OrgChat.Core.Entities;

public class Department : BaseEntity
{
    public int OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Responsable désigné du département (optionnel).</summary>
    public int? ManagerUserId { get; set; }
    public User? Manager { get; set; }

    public ICollection<DepartmentMember> Members { get; set; } = new List<DepartmentMember>();
}
