namespace OrgChat.Core.Entities;

public class Attachment : BaseEntity
{
    public int MessageId { get; set; }
    public Message Message { get; set; } = null!;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StorageFileName { get; set; } = string.Empty; // nom généré par le serveur
    public string ContentType { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
}
