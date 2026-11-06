using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OrgChat.Core.Contracts.Conversations;
using OrgChat.Core.Contracts.Messages;
using OrgChat.Core.Contracts.Realtime;
using OrgChat.Server.Services;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace OrgChat.Server.Hubs;

[Authorize]
public class ChatHub : Hub
{
    // Un utilisateur peut avoir plusieurs connexions (plusieurs postes ou reconnexions).
    private static readonly ConcurrentDictionary<int, int> OnlineConnections = new();
    private static readonly ConcurrentDictionary<int, ConcurrentDictionary<string, byte>> UserConnections = new();

    private readonly MessageService _messageService;
    private readonly ConversationService _conversationService;

    public ChatHub(MessageService messageService, ConversationService conversationService)
    {
        _messageService = messageService;
        _conversationService = conversationService;
    }

    public static string UserGroup(int userId) => $"user-{userId}";

    /// <summary>
    /// Retire toutes les connexions actives d'un utilisateur du groupe d'une
    /// conversation. Utilisé lorsque l'utilisateur quitte ou est retiré d'un
    /// groupe : la base de données ne suffit pas, le groupe SignalR doit aussi
    /// être nettoyé pour éviter de continuer à diffuser des messages.
    /// </summary>
    public static async Task RemoveUserFromGroupAsync(
        IHubContext<ChatHub> hub,
        int userId,
        int conversationId)
    {
        if (!UserConnections.TryGetValue(userId, out var connections))
            return;

        foreach (var connectionId in connections.Keys)
        {
            try
            {
                await hub.Groups.RemoveFromGroupAsync(connectionId, conversationId.ToString());
            }
            catch
            {
                // La connexion a pu se fermer entre-temps : rien à faire.
            }
        }
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        if (userId > 0)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));

            var userConnections = UserConnections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
            userConnections[Context.ConnectionId] = 0;

            var connections = OnlineConnections.AddOrUpdate(userId, 1, (_, current) => current + 1);
            if (connections == 1)
            {
                await Clients.All.SendAsync("PresenceChanged", new PresenceChangedDto
                {
                    UserId = userId,
                    Username = GetUsername(),
                    IsOnline = true
                });
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        if (userId > 0)
        {
            var connections = OnlineConnections.AddOrUpdate(userId, 0, (_, current) => Math.Max(0, current - 1));
            if (connections == 0)
            {
                OnlineConnections.TryRemove(userId, out _);

                await Clients.All.SendAsync("PresenceChanged", new PresenceChangedDto
                {
                    UserId = userId,
                    Username = GetUsername(),
                    IsOnline = false
                });
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public IReadOnlyCollection<int> GetOnlineUsers() =>
        OnlineConnections
            .Where(pair => pair.Value > 0)
            .Select(pair => pair.Key)
            .ToArray();

    public async Task StartTyping(int conversationId)
    {
        await BroadcastTypingAsync(conversationId, isTyping: true);
    }

    public async Task StopTyping(int conversationId)
    {
        await BroadcastTypingAsync(conversationId, isTyping: false);
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

    public async Task<ReadReceiptDto?> MarkAsRead(int conversationId)
    {
        var userId = GetCurrentUserId();
        var receipt = await _conversationService.MarkAsReadAsync(conversationId, userId);
        if (receipt is null)
            return null;

        await Clients.Group(conversationId.ToString()).SendAsync("ConversationRead", receipt);
        return receipt;
    }

    public async Task<PagedMessageResponse> GetHistory(int conversationId, int page, int pageSize)
    {
        var userId = GetCurrentUserId();
        return await _messageService.GetMessagesAsync(conversationId, userId, page, pageSize);
    }

    private async Task BroadcastTypingAsync(int conversationId, bool isTyping)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0 || !await _conversationService.IsParticipantAsync(conversationId, userId))
            return;

        await Clients.OthersInGroup(conversationId.ToString()).SendAsync("UserTyping", new UserTypingDto
        {
            ConversationId = conversationId,
            UserId = userId,
            Username = GetUsername(),
            IsTyping = isTyping
        });
    }

    private string GetUsername() =>
        Context.User?.FindFirstValue(ClaimTypes.Name) ?? "Utilisateur";

    private int GetCurrentUserId()
    {
        var userIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdClaim != null ? int.Parse(userIdClaim) : 0;
    }
}
