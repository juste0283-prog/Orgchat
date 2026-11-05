using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Contracts.Messages;
using OrgChat.Core.Entities;
using OrgChat.Infrastructure.Data;

namespace OrgChat.Server.Services;

public class MessageService
{
    private readonly AppDbContext _db;

    public MessageService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MessageDto> SendMessageAsync(int conversationId, int senderId, MessageCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new InvalidOperationException("Le contenu du message est requis.");

        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == senderId);
        if (!isParticipant)
            throw new InvalidOperationException("Vous ne faites pas partie de cette conversation.");

        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow, // horodatage serveur, jamais fourni par le client
            IsDeleted = false
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();

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
                DeletedAt = m.DeletedAt
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

    private async Task<MessageDto?> GetMessageDtoAsync(int messageId)
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
                DeletedAt = m.DeletedAt
            })
            .FirstOrDefaultAsync();
    }
}
