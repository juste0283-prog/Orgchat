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

    public async Task<PagedMessageResponse> GetMessagesAsync(int conversationId, int userId, int page, int pageSize)
    {
        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);
        if (!isParticipant)
            return new PagedMessageResponse { Page = page, PageSize = pageSize };

        var query = _db.Messages
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt);

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
