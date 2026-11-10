namespace OrgChat.Core.Contracts.Realtime;

public class PresenceChangedDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
}
