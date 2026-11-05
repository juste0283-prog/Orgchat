namespace OrgChat.Core.Contracts.Conversations;

public class ConversationParticipantDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public DateTime? LastReadAt { get; set; }
    public bool IsGroupAdmin { get; set; }
}
