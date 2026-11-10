using OrgChat.Core.Enums;

namespace OrgChat.Core.Entities;

public class Call : BaseEntity
{
    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public int InitiatorId { get; set; }
    public User Initiator { get; set; } = null!;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public CallStatus Status { get; set; } = CallStatus.Ringing;
}
