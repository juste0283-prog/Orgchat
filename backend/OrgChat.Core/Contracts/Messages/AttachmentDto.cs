namespace OrgChat.Core.Contracts.Messages;

public class AttachmentDto
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
}
