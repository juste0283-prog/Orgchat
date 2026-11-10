namespace OrgChat.Core.Contracts.Conversations;

/// <summary>Accusé de lecture d'un participant pour une conversation.</summary>
public class ReadReceiptDto
{
    public int ConversationId { get; set; }
    public int UserId { get; set; }
    public DateTime ReadAt { get; set; }
}
