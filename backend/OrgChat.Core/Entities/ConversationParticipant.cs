using System.Collections.Generic;

namespace OrgChat.Core.Entities;

public class ConversationParticipant : BaseEntity
{
    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastReadAt { get; set; }
    public bool IsGroupAdmin { get; set; }
}
