namespace OrgChat.Core.Contracts.Users;

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public int OrganizationId { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    /// <summary>Statut métier : Active | Suspended | Disabled.</summary>
    public string Status { get; set; } = "Active";
    public DateTime? SuspendedUntil { get; set; }
    public string? SuspensionReason { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsOnline { get; set; }
}
