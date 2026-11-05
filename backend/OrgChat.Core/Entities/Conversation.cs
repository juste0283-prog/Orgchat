using OrgChat.Core.Enums;
using System.Collections.Generic;

namespace OrgChat.Core.Entities;

public class Conversation : BaseEntity
{
    public string? Title { get; set; }
    public ConversationType Type { get; set; }
    public int CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public ICollection<ConversationParticipant> Participants { get; set; } = new List<ConversationParticipant>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
