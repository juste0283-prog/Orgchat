using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OrgChat.Core.Contracts.Messages;
using OrgChat.Server.Services;

namespace OrgChat.Server.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly MessageService _messageService;
    private readonly ConversationService _conversationService;

    public ChatHub(MessageService messageService, ConversationService conversationService)
    {
        _messageService = messageService;
        _conversationService = conversationService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        await base.OnConnectedAsync();
    }

    public async Task SendMessage(int conversationId, string content)
    {
        var senderId = GetCurrentUserId();

        var message = await _messageService.SendMessageAsync(conversationId, senderId, new MessageCreateRequest { Content = content });

        // Broadcast uniquement aux participants de la conversation via un groupe SignalR
        await Clients.Group(conversationId.ToString()).SendAsync("ReceiveMessage", message);
    }

    public async Task JoinConversation(int conversationId)
    {
        var userId = GetCurrentUserId();

        var conversation = await _conversationService.GetConversationByIdAsync(conversationId, userId);
        if (conversation == null)
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId.ToString());
    }

    public async Task LeaveConversation(int conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId.ToString());
    }

    public async Task<PagedMessageResponse> GetHistory(int conversationId, int page, int pageSize)
    {
        var userId = GetCurrentUserId();
        return await _messageService.GetMessagesAsync(conversationId, userId, page, pageSize);
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = Context.User?.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        return userIdClaim != null ? int.Parse(userIdClaim) : 0;
    }
}
