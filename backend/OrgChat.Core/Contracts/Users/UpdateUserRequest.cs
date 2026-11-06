namespace OrgChat.Core.Contracts.Users;

public class UpdateUserRequest
{
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? JobTitle { get; set; }
    public int? DepartmentId { get; set; }
    public bool? ClearDepartment { get; set; }
}
