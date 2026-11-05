namespace OrgChat.Core.Contracts.Conversations;

public class AddParticipantRequest
{
    public int UserId { get; set; }
    public bool IsGroupAdmin { get; set; } = false;
}
