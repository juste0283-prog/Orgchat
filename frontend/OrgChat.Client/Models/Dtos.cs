using OrgChat.Client.Services;

namespace OrgChat.Client.Models;

public sealed class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class ActivateAccountRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public bool IsAdmin => Role is "OrgAdmin" or "SuperAdmin";
}

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsOnline { get; set; }
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public int OrganizationId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime? SuspendedUntil { get; set; }
    public string? SuspensionReason { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public bool IsAdmin => Role is "OrgAdmin" or "SuperAdmin";

    public string Initials => BuildInitials(DisplayName, Username);

    internal static string BuildInitials(string? displayName, string? username)
    {
        var value = string.IsNullOrWhiteSpace(displayName) ? username : displayName;
        if (string.IsNullOrWhiteSpace(value))
            return "?";

        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[1][0]));

        return value.Length >= 2
            ? value[..2].ToUpperInvariant()
            : value.ToUpperInvariant();
    }
}

public sealed class ConversationParticipantDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public DateTime? LastReadAt { get; set; }
    public bool IsGroupAdmin { get; set; }

    public string Initials => UserDto.BuildInitials(DisplayName, Username);
}

public sealed class ConversationDto
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string Type { get; set; } = "Private";
    public int CreatedById { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<ConversationParticipantDto> Participants { get; set; } = new();
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }
    public int UnreadCount { get; set; }

    public bool IsGroup => string.Equals(Type, "Group", StringComparison.OrdinalIgnoreCase);

    public string DisplayTitle(int currentUserId)
    {
        if (IsGroup)
            return string.IsNullOrWhiteSpace(Title) ? Localization.F("Groupe #{0}", Id) : Title!;

        var other = Participants.FirstOrDefault(p => p.UserId != currentUserId);
        return other?.DisplayName is { Length: > 0 }
            ? other.DisplayName
            : other?.Username ?? Localization.F("Conversation #{0}", Id);
    }

    public string ParticipantSummary(int currentUserId)
    {
        if (IsGroup)
        {
            var names = Participants
                .Where(p => p.UserId != currentUserId)
                .Select(p => p.DisplayName is { Length: > 0 } ? p.DisplayName : p.Username)
                .Take(3)
                .ToList();

            if (names.Count == 0)
                return Localization.F("{0} participants", Participants.Count);
            var suffix = Participants.Count > 4 ? $" +{Participants.Count - 4}" : string.Empty;
            return Localization.F("{0} participants · {1}", Participants.Count, string.Join(", ", names) + suffix);
        }

        return Localization.T("Conversation privée");
    }

    public string PreviewText => string.IsNullOrWhiteSpace(LastMessagePreview)
        ? Localization.T("Aucun message pour le moment")
        : LastMessagePreview!;
}

public sealed class ReadReceiptDto
{
    public int ConversationId { get; set; }
    public int UserId { get; set; }
    public DateTime ReadAt { get; set; }
}

public sealed class AttachmentDto
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;

    public string DisplaySize
    {
        get
        {
            if (SizeInBytes < 1024)
                return $"{SizeInBytes} {Localization.T("o")}";
            if (SizeInBytes < 1024 * 1024)
                return $"{SizeInBytes / 1024d:0.#} {Localization.T("Ko")}";
            return $"{SizeInBytes / (1024d * 1024d):0.#} {Localization.T("Mo")}";
        }
    }
}

public sealed class MessageDto
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public int SenderId { get; set; }
    public string SenderUsername { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public List<AttachmentDto> Attachments { get; set; } = new();

    public DateTime LocalCreatedAt => CreatedAt.Kind == DateTimeKind.Utc
        ? CreatedAt.ToLocalTime()
        : DateTime.SpecifyKind(CreatedAt, DateTimeKind.Local);

    public string Preview => string.IsNullOrWhiteSpace(Content) && Attachments.Count > 0
        ? "ðŸ“Ž " + Attachments[0].OriginalFileName
        : Content;
}

public sealed class NotificationDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public int ConversationId { get; set; }
    public int MessageId { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string ConversationTitle { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsRead => ReadAt is not null;
}

public sealed class NotificationCountDto
{
    public int UnreadCount { get; set; }
}

public sealed class PresenceChangedDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
}

public sealed class UserTypingDto
{
    public int ConversationId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsTyping { get; set; }
}

public sealed class PagedMessageResponse
{
    public List<MessageDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public bool HasNextPage { get; set; }
}

public sealed class ConversationCreateRequest
{
    public string? Title { get; set; }
    public string Type { get; set; } = "Private";
    public List<int> ParticipantUserIds { get; set; } = new();
}

public sealed class AddParticipantRequest
{
    public int UserId { get; set; }
    public bool IsGroupAdmin { get; set; }
}

public sealed class UpdateUserRequest
{
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
}

public sealed class MessageCreateRequest
{
    public string Content { get; set; } = string.Empty;
}

public sealed class MessageUpdateRequest
{
    public string Content { get; set; } = string.Empty;
}

public sealed class MessageDeletedDto
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public DateTime DeletedAt { get; set; }
}

public sealed class ConversationDeletedDto
{
    public int Id { get; set; }
    public DateTime DeletedAt { get; set; }
}

public sealed class MessageSearchResultDto
{
    public int MessageId { get; set; }
    public int ConversationId { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string ConversationTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public DateTime LocalCreatedAt => CreatedAt.Kind == DateTimeKind.Utc
        ? CreatedAt.ToLocalTime()
        : DateTime.SpecifyKind(CreatedAt, DateTimeKind.Local);
}

public sealed class CreateUserRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    public int? OrganizationId { get; set; }
    // Compatibilité : rempli uniquement si créé en direct (ancien flux)
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class CreatedUserDto : UserDto
{
    public string? ActivationToken { get; set; }
    public string? ActivationLink { get; set; }
    public DateTime? InvitationExpiresAt { get; set; }
}

public sealed class UpdateUserRoleRequest
{
    public string Role { get; set; } = "User";
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class OrganizationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public int UserCount { get; set; }
    public int DepartmentCount { get; set; }
}

public sealed class DepartmentDto
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

public sealed class DepartmentMemberDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime JoinedAt { get; set; }
}

public sealed class DepartmentDetailDto
{
    public DepartmentDto Department { get; set; } = new();
    public List<DepartmentMemberDto> Members { get; set; } = new();
}

public sealed class DepartmentCreateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ManagerUserId { get; set; }
}

public sealed class DepartmentUpdateRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? ManagerUserId { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class AddDepartmentMemberRequest
{
    public int UserId { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class DepartmentStatsDto
{
    public int DepartmentId { get; set; }
    public int MemberCount { get; set; }
    public int ActiveUsers { get; set; }
    public int OnlineUsers { get; set; }
    public int MessageCount { get; set; }
    public int ConversationCount { get; set; }
}

// ---------------------------------------------------------------- Supervision

public sealed class SupervisionSummaryDto
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

public sealed class SupervisionUserDto
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

public sealed class DepartmentSupervisionDto
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

public sealed class ActivityPointDto
{
    public DateTime Date { get; set; }
    public int Logins { get; set; }
    public int FailedLogins { get; set; }
    public int NewUsers { get; set; }
    public int Messages { get; set; }
    public int ActiveUsers { get; set; }
}

public sealed class ConnectionPointDto
{
    public DateTime PeriodStart { get; set; }
    public int Sessions { get; set; }
    public int UniqueUsers { get; set; }
}

public sealed class SupervisionConnectionsDto
{
    public int CurrentConcurrentConnections { get; set; }
    public int CurrentOnlineUsers { get; set; }
    public List<ConnectionPointDto> Points { get; set; } = new();
}

public sealed class SupervisionHealthDto
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

public sealed class IncidentDto
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

public sealed class TopActiveUserDto
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int ActivityCount { get; set; }
}

public sealed class SupervisionReportDto
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

public sealed class AuditLogDto
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

public sealed class AuditFacetsDto
{
    public List<string> Actions { get; set; } = new();
    public List<string> EntityTypes { get; set; } = new();
    public List<string> Results { get; set; } = new();
}

public sealed class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

