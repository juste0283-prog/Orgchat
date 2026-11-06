using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Contracts.Notifications;
using OrgChat.Core.Entities;
using OrgChat.Core.Enums;
using OrgChat.Infrastructure.Data;
using OrgChat.Server.Hubs;

namespace OrgChat.Server.Services;

/// <summary>
/// Persiste et expose les notifications utilisateur.
/// Le payload JSON reste volontairement simple afin de ne pas modifier le schéma existant.
/// </summary>
public class NotificationService
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hub;

    public NotificationService(AppDbContext db, IHubContext<ChatHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task<IReadOnlyList<NotificationDto>> CreateMessageNotificationsAsync(Message message, int senderId)
    {
        var conversation = await _db.Conversations
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(c => c.Id == message.ConversationId);

        if (conversation is null)
            return Array.Empty<NotificationDto>();

        var sender = conversation.Participants.FirstOrDefault(p => p.UserId == senderId)?.User;
        var senderName = string.IsNullOrWhiteSpace(sender?.DisplayName)
            ? sender?.Username ?? "Quelqu'un"
            : sender!.DisplayName;
        var conversationTitle = conversation.Type == ConversationType.Group
            ? string.IsNullOrWhiteSpace(conversation.Title) ? $"Groupe #{conversation.Id}" : conversation.Title!
            : senderName;
        var preview = BuildPreview(message);

        var recipients = conversation.Participants
            .Where(p => p.UserId != senderId)
            .Select(p => p.UserId)
            .Distinct()
            .ToList();

        if (recipients.Count == 0)
            return Array.Empty<NotificationDto>();

        var entities = recipients.Select(userId => new Notification
        {
            UserId = userId,
            Type = "Message",
            Payload = JsonSerializer.Serialize(new MessageNotificationPayload
            {
                ConversationId = message.ConversationId,
                MessageId = message.Id,
                SenderId = senderId,
                SenderName = senderName,
                ConversationTitle = conversationTitle,
                Preview = preview
            }, PayloadJsonOptions),
            CreatedAt = message.CreatedAt
        }).ToList();

        _db.Notifications.AddRange(entities);
        await _db.SaveChangesAsync();

        var dtos = entities.Select(ToDto).ToList();
        foreach (var dto in dtos)
        {
            try
            {
                // Groupe explicite par utilisateur : indépendant du mapping IUserIdProvider.
                await _hub.Clients.Group(ChatHub.UserGroup(dto.UserId))
                    .SendAsync("NotificationReceived", dto);
            }
            catch
            {
                // La notification reste persistée même si la diffusion temps réel échoue.
            }
        }

        return dtos;
    }

    public async Task<ICollection<NotificationDto>> GetForUserAsync(int userId, int take = 100, int skip = 0)
    {
        take = Math.Clamp(take, 1, 200);
        skip = Math.Max(0, skip);

        var notifications = await _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return notifications.Select(ToDto).ToList();
    }

    public async Task<bool> DeleteAsync(int notificationId, int userId)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification is null)
            return false;

        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> ClearAsync(int userId)
    {
        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId)
            .ToListAsync();

        if (notifications.Count == 0)
            return 0;

        _db.Notifications.RemoveRange(notifications);
        await _db.SaveChangesAsync();
        return notifications.Count;
    }

    public async Task<int> PurgeReadBeforeAsync(int userId, DateTime cutoff)
    {
        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt != null && n.CreatedAt < cutoff)
            .ToListAsync();

        if (notifications.Count == 0)
            return 0;

        _db.Notifications.RemoveRange(notifications);
        await _db.SaveChangesAsync();
        return notifications.Count;
    }

    public async Task<int> PurgeAllReadBeforeAsync(DateTime cutoff)
    {
        var notifications = await _db.Notifications
            .Where(n => n.ReadAt != null && n.CreatedAt < cutoff)
            .ToListAsync();

        if (notifications.Count == 0)
            return 0;

        _db.Notifications.RemoveRange(notifications);
        await _db.SaveChangesAsync();
        return notifications.Count;
    }

    public Task<int> GetUnreadCountAsync(int userId) =>
        _db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null);

    public async Task<NotificationDto?> MarkAsReadAsync(int notificationId, int userId)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification is null)
            return null;

        if (notification.ReadAt is null)
        {
            notification.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return ToDto(notification);
    }

    public async Task<int> MarkAllAsReadAsync(int userId)
    {
        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ToListAsync();

        if (notifications.Count == 0)
            return 0;

        var readAt = DateTime.UtcNow;
        foreach (var notification in notifications)
            notification.ReadAt = readAt;

        await _db.SaveChangesAsync();
        return notifications.Count;
    }

    public async Task<int> DeleteForMessageAsync(int messageId)
    {
        var notifications = await _db.Notifications
            .Where(n => n.Type == "Message")
            .ToListAsync();

        var matching = notifications
            .Where(n => TryReadPayload(n.Payload)?.MessageId == messageId)
            .ToList();

        if (matching.Count == 0)
            return 0;

        _db.Notifications.RemoveRange(matching);
        await _db.SaveChangesAsync();
        return matching.Count;
    }

    public async Task<int> DeleteForConversationAsync(int conversationId)
    {
        var notifications = await _db.Notifications
            .Where(n => n.Type == "Message")
            .ToListAsync();

        var matching = notifications
            .Where(n => TryReadPayload(n.Payload)?.ConversationId == conversationId)
            .ToList();

        if (matching.Count == 0)
            return 0;

        _db.Notifications.RemoveRange(matching);
        await _db.SaveChangesAsync();
        return matching.Count;
    }

    public async Task<int> DeleteForUserConversationAsync(int userId, int conversationId)
    {
        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId && n.Type == "Message")
            .ToListAsync();

        var matching = notifications
            .Where(n => TryReadPayload(n.Payload)?.ConversationId == conversationId)
            .ToList();

        if (matching.Count == 0)
            return 0;

        _db.Notifications.RemoveRange(matching);
        await _db.SaveChangesAsync();
        return matching.Count;
    }

    public async Task<int> MarkConversationAsReadAsync(int userId, int conversationId, DateTime readAt)
    {
        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ToListAsync();

        var matching = notifications
            .Where(n => TryReadPayload(n.Payload)?.ConversationId == conversationId)
            .ToList();

        if (matching.Count == 0)
            return 0;

        foreach (var notification in matching)
            notification.ReadAt = readAt;

        await _db.SaveChangesAsync();
        return matching.Count;
    }

    private static NotificationDto ToDto(Notification notification)
    {
        var payload = TryReadPayload(notification.Payload);

        return new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            Type = notification.Type,
            ConversationId = payload?.ConversationId ?? 0,
            MessageId = payload?.MessageId ?? 0,
            SenderId = payload?.SenderId ?? 0,
            SenderName = payload?.SenderName ?? string.Empty,
            ConversationTitle = payload?.ConversationTitle ?? string.Empty,
            Preview = payload?.Preview ?? string.Empty,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt
        };
    }

    private static string BuildPreview(Message message)
    {
        var text = message.Content?.Trim() ?? string.Empty;
        if (text.Length == 0 && message.Attachments.Count > 0)
            text = "Pièce jointe : " + message.Attachments.First().OriginalFileName;

        return text.Length <= 180 ? text : text[..177] + "…";
    }

    private static MessageNotificationPayload? TryReadPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            return JsonSerializer.Deserialize<MessageNotificationPayload>(payload, PayloadJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class MessageNotificationPayload
    {
        public int ConversationId { get; set; }
        public int MessageId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string ConversationTitle { get; set; } = string.Empty;
        public string Preview { get; set; } = string.Empty;
    }
}
