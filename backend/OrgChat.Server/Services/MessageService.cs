using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Contracts.Messages;
using OrgChat.Core.Entities;
using OrgChat.Infrastructure.Data;

namespace OrgChat.Server.Services;

public class MessageService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;

    public MessageService(AppDbContext db, NotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<MessageDto> SendMessageAsync(int conversationId, int senderId, MessageCreateRequest request)
    {
        return await CreateMessageAsync(conversationId, senderId, request.Content, attachment: null);
    }

    public async Task<MessageDto> SendAttachmentMessageAsync(int conversationId, int senderId, string? content, Attachment attachment)
    {
        if (attachment is null)
            throw new InvalidOperationException("La pièce jointe est requise.");

        return await CreateMessageAsync(conversationId, senderId, content, attachment);
    }

    private async Task<MessageDto> CreateMessageAsync(int conversationId, int senderId, string? content, Attachment? attachment)
    {
        var trimmed = content?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 && attachment is null)
            throw new InvalidOperationException("Le contenu du message est requis.");

        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == senderId);
        if (!isParticipant)
            throw new InvalidOperationException("Vous ne faites pas partie de cette conversation.");

        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = trimmed,
            CreatedAt = DateTime.UtcNow, // horodatage serveur, jamais fourni par le client
            IsDeleted = false
        };

        if (attachment is not null)
            message.Attachments.Add(attachment);

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

        // Les notifications sont créées après le message pour ne jamais référencer un message absent.
        await _notifications.CreateMessageNotificationsAsync(message, senderId);

        return await GetMessageDtoAsync(message.Id)
            ?? throw new InvalidOperationException("Impossible de récupérer le message envoyé.");
    }

    public async Task<MessageDto?> UpdateMessageAsync(
        int conversationId,
        int messageId,
        int userId,
        string? content)
    {
        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);
        if (!isParticipant)
            return null;

        var message = await _db.Messages
            .Include(m => m.Attachments)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversationId);

        if (message is null || message.IsDeleted)
            return null;

        if (message.SenderId != userId)
            throw new UnauthorizedAccessException("Vous ne pouvez modifier que vos propres messages.");

        var trimmed = content?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 && message.Attachments.Count == 0)
            throw new InvalidOperationException("Le contenu du message est requis.");

        if (string.Equals(message.Content, trimmed, StringComparison.Ordinal))
            return await GetMessageDtoAsync(message.Id);

        message.Content = trimmed;
        message.EditedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await GetMessageDtoAsync(message.Id);
    }

    public async Task<MessageDeletedDto?> DeleteMessageAsync(int conversationId, int messageId, int userId)
    {
        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);
        if (!isParticipant)
            return null;

        var message = await _db.Messages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversationId);

        if (message is null || message.IsDeleted)
            return null;

        if (message.SenderId != userId)
            throw new UnauthorizedAccessException("Vous ne pouvez supprimer que vos propres messages.");

        var deletedAt = DateTime.UtcNow;
        message.IsDeleted = true;
        message.DeletedAt = deletedAt;
        message.Content = string.Empty;
        message.EditedAt = null;

        await _db.SaveChangesAsync();

        // Les notifications historiques ne doivent plus pointer vers un message supprimé.
        await _notifications.DeleteForMessageAsync(message.Id);

        return new MessageDeletedDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            DeletedAt = deletedAt
        };
    }

    public async Task<PagedMessageResponse> GetMessagesAsync(int conversationId, int userId, int page, int pageSize)
    {
        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);
        if (!isParticipant)
            return new PagedMessageResponse { Page = page, PageSize = pageSize };

        var query = _db.Messages
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderUsername = m.Sender.Username,
                Content = m.Content,
                CreatedAt = m.CreatedAt,
                EditedAt = m.EditedAt,
                IsDeleted = m.IsDeleted,
                DeletedAt = m.DeletedAt,
                Attachments = m.Attachments
                    .OrderBy(a => a.Id)
                    .Select(a => new AttachmentDto
                    {
                        Id = a.Id,
                        MessageId = a.MessageId,
                        OriginalFileName = a.OriginalFileName,
                        ContentType = a.ContentType,
                        SizeInBytes = a.SizeInBytes,
                        DownloadUrl = "/api/attachments/" + a.Id
                    })
                    .ToList()
            })
            .ToListAsync();

        return new PagedMessageResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            HasNextPage = page * pageSize < totalCount
        };
    }

    public async Task<IReadOnlyList<MessageSearchResultDto>> SearchAsync(int userId, string query, int take = 50)
    {
        query = query?.Trim() ?? string.Empty;
        if (query.Length < 2)
            return Array.Empty<MessageSearchResultDto>();

        take = Math.Clamp(take, 1, 100);

        var messages = await _db.Messages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Include(m => m.Conversation)
                .ThenInclude(c => c.Participants)
                    .ThenInclude(p => p.User)
            .Where(m => !m.IsDeleted &&
                        m.Content.Contains(query) &&
                        m.Conversation.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .ToListAsync();

        return messages.Select(m => new MessageSearchResultDto
        {
            MessageId = m.Id,
            ConversationId = m.ConversationId,
            SenderId = m.SenderId,
            SenderName = string.IsNullOrWhiteSpace(m.Sender.DisplayName)
                ? m.Sender.Username
                : m.Sender.DisplayName,
            ConversationTitle = ResolveConversationTitle(m.Conversation, userId),
            Content = m.Content,
            CreatedAt = m.CreatedAt
        }).ToList();
    }

    private static string ResolveConversationTitle(Conversation conversation, int userId)
    {
        if (conversation.Type == OrgChat.Core.Enums.ConversationType.Group)
            return string.IsNullOrWhiteSpace(conversation.Title)
                ? $"Groupe #{conversation.Id}"
                : conversation.Title!;

        var other = conversation.Participants.FirstOrDefault(p => p.UserId != userId)?.User;
        return string.IsNullOrWhiteSpace(other?.DisplayName)
            ? other?.Username ?? $"Conversation #{conversation.Id}"
            : other.DisplayName;
    }

    public async Task<MessageDto?> GetMessageDtoAsync(int messageId)
    {
        return await _db.Messages
            .Where(m => m.Id == messageId)
            .Select(m => new MessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderUsername = m.Sender.Username,
                Content = m.Content,
                CreatedAt = m.CreatedAt,
                EditedAt = m.EditedAt,
                IsDeleted = m.IsDeleted,
                DeletedAt = m.DeletedAt,
                Attachments = m.Attachments
                    .OrderBy(a => a.Id)
                    .Select(a => new AttachmentDto
                    {
                        Id = a.Id,
                        MessageId = a.MessageId,
                        OriginalFileName = a.OriginalFileName,
                        ContentType = a.ContentType,
                        SizeInBytes = a.SizeInBytes,
                        DownloadUrl = "/api/attachments/" + a.Id
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();
    }
}
