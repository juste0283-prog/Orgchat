namespace OrgChat.Core.Contracts.Supervision;

/// <summary>
/// Contrats du module de supervision. Toutes les données exposées sont des
/// métadonnées (comptes, connexions, volumes) : le contenu des conversations
/// privées n'est jamais retourné par ces endpoints.
/// </summary>
public class SupervisionSummaryDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int SuspendedUsers { get; set; }
    public int DisabledUsers { get; set; }
    public int OnlineUsers { get; set; }
    public int Departments { get; set; }
    public int Conversations { get; set; }
    public int MessagesLast24Hours { get; set; }
    public int NewUsersLast7Days { get; set; }
    public int ActiveSessions { get; set; }
    public int ConcurrentConnections { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class SupervisionUserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string Status { get; set; } = "Active";
    public bool IsActive { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? SuspendedUntil { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DepartmentSupervisionDto
{
    public int DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? ManagerName { get; set; }
    public int MemberCount { get; set; }
    public int ActiveUsers { get; set; }
    public int OnlineUsers { get; set; }
    public int MessagesLast7Days { get; set; }
}

public class ActivityPointDto
{
    public DateTime Date { get; set; }
    public int Logins { get; set; }
    public int FailedLogins { get; set; }
    public int NewUsers { get; set; }
    public int Messages { get; set; }
    public int ActiveUsers { get; set; }
}

public class ConnectionPointDto
{
    public DateTime PeriodStart { get; set; }
    public int Sessions { get; set; }
    public int UniqueUsers { get; set; }
}

public class SupervisionHealthDto
{
    public string Status { get; set; } = "Healthy";
    public string Api { get; set; } = "Healthy";
    public string Database { get; set; } = "Healthy";
    public string SignalR { get; set; } = "Healthy";
    public string DatabaseProvider { get; set; } = string.Empty;
    public long UptimeSeconds { get; set; }
    public DateTime ServerTimeUtc { get; set; }
    public string Version { get; set; } = string.Empty;
    public int ConcurrentConnections { get; set; }
    public int OnlineUsers { get; set; }
    public int ActiveSessions { get; set; }
}

public class IncidentDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ActorUserId { get; set; }
    public string ActorName { get; set; } = "Système";
    public string Action { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
}

public class TopActiveUserDto
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int ActivityCount { get; set; }
}

public class SupervisionReportDto
{
    public string Period { get; set; } = "daily";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int NewUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int Logins { get; set; }
    public int Messages { get; set; }
    public int ConversationsCreated { get; set; }
    public int SuspendedAccounts { get; set; }
    public int Incidents { get; set; }
    public List<TopActiveUserDto> TopUsers { get; set; } = new();
    public List<DepartmentSupervisionDto> Departments { get; set; } = new();
}

public class AuditLogDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ActorUserId { get; set; }
    public string ActorName { get; set; } = "Système";
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
}

public class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (TotalCount + PageSize - 1) / PageSize;
}
