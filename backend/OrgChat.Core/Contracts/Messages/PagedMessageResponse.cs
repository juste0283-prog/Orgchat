namespace OrgChat.Core.Contracts.Messages;

public class PagedMessageResponse
{
    public ICollection<MessageDto> Items { get; set; } = new List<MessageDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public bool HasNextPage { get; set; }
}
