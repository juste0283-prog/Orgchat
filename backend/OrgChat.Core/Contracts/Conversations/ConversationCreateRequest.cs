namespace OrgChat.Core.Contracts.Conversations;

public class ConversationCreateRequest
{
    public string? Title { get; set; }
    public string Type { get; set; } = "Private"; // "Private" | "Group"
    public ICollection<int> ParticipantUserIds { get; set; } = new List<int>();
}
