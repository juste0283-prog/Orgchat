namespace OrgChat.Core.Entities;

public class Notification : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Type { get; set; } = string.Empty;
    public string? Payload { get; set; } // contenu JSON, sans secret
    public DateTime? ReadAt { get; set; }
}
