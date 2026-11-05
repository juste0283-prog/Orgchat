namespace OrgChat.Core.Contracts.Conversations;

public class ConversationDto
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string Type { get; set; } = string.Empty; // "Private" | "Group"
    public int CreatedById { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public ICollection<ConversationParticipantDto> Participants { get; set; } = new List<ConversationParticipantDto>();
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }
}
