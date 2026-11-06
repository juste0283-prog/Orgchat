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

public sealed class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
}

public sealed class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);

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
            return string.IsNullOrWhiteSpace(Title) ? $"Groupe #{Id}" : Title!;

        var other = Participants.FirstOrDefault(p => p.UserId != currentUserId);
        return other?.DisplayName is { Length: > 0 }
            ? other.DisplayName
            : other?.Username ?? $"Conversation #{Id}";
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
                return $"{Participants.Count} participants";
            var suffix = Participants.Count > 4 ? $" +{Participants.Count - 4}" : string.Empty;
            return $"{Participants.Count} participants · {string.Join(", ", names)}{suffix}";
        }

        return "Conversation privée";
    }

    public string PreviewText => string.IsNullOrWhiteSpace(LastMessagePreview)
        ? "Aucun message pour le moment"
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
                return $"{SizeInBytes} o";
            if (SizeInBytes < 1024 * 1024)
                return $"{SizeInBytes / 1024d:0.#} Ko";
            return $"{SizeInBytes / (1024d * 1024d):0.#} Mo";
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
        ? "📎 " + Attachments[0].OriginalFileName
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
