using System.Collections.Generic;

namespace OrgChat.Core.Entities;

public class Organization : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Paramètres de l'organisation, sérialisés en JSON (extensible).</summary>
    public string? SettingsJson { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Department> Departments { get; set; } = new List<Department>();
}
