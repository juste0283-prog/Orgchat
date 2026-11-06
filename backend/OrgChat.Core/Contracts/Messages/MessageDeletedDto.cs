namespace OrgChat.Core.Contracts.Messages;

public class MessageDeletedDto
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public DateTime DeletedAt { get; set; }
}
