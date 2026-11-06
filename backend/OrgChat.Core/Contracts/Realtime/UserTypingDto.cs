namespace OrgChat.Core.Contracts.Realtime;

public class UserTypingDto
{
    public int ConversationId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsTyping { get; set; }
}
