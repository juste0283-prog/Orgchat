namespace OrgChat.Core.Contracts.Departments;

public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int OrganizationId { get; set; }
    public int? ManagerUserId { get; set; }
    public string? ManagerName { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DepartmentCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ManagerUserId { get; set; }
}

public class DepartmentUpdateRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? ManagerUserId { get; set; }
    public bool? IsActive { get; set; }
}

public class DepartmentMemberDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class AddDepartmentMemberRequest
{
    public int UserId { get; set; }
    public bool IsPrimary { get; set; }
}

public class DepartmentStatsDto
{
    public int DepartmentId { get; set; }
    public int MemberCount { get; set; }
    public int ActiveUsers { get; set; }
    public int OnlineUsers { get; set; }
    public int MessageCount { get; set; }
    public int ConversationCount { get; set; }
}
