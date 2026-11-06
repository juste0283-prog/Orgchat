using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Contracts.Conversations;
using OrgChat.Core.Contracts.Messages;
using OrgChat.Core.Entities;
using OrgChat.Core.Enums;
using OrgChat.Infrastructure.Data;

namespace OrgChat.Server.Services;

public class ConversationService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;

    public ConversationService(AppDbContext db, NotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<ICollection<ConversationDto>> GetConversationsForUserAsync(int userId)
    {
        var conversations = await _db.Conversations
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
            .Include(c => c.Messages)
                .ThenInclude(m => m.Attachments)
            .Where(c => c.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return conversations.Select(c => MapToDto(c, userId)).ToList();
    }

    public async Task<ConversationDto?> GetConversationByIdAsync(int conversationId, int userId)
    {
        var conversation = await _db.Conversations
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
            .Include(c => c.Messages)
                .ThenInclude(m => m.Attachments)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null)
            return null;

        var isParticipant = conversation.Participants.Any(p => p.UserId == userId);
        if (!isParticipant)
            return null;

        return MapToDto(conversation, userId);
    }

    public async Task<ConversationDto> CreateConversationAsync(ConversationCreateRequest request, int creatorId)
    {
        var type = Enum.TryParse<ConversationType>(request.Type, ignoreCase: true, out var parsedType)
            ? parsedType
            : ConversationType.Private;

        if (type == ConversationType.Private && request.ParticipantUserIds.Count != 1)
            throw new InvalidOperationException("Une conversation privée doit avoir exactement deux participants.");

        if (type == ConversationType.Group && request.ParticipantUserIds.Count < 1)
            throw new InvalidOperationException("Un groupe doit avoir au moins deux participants.");

        var allUserIds = new HashSet<int>(request.ParticipantUserIds) { creatorId };

        var usersExist = await _db.Users
            .Where(u => allUserIds.Contains(u.Id) && u.IsActive)
            .CountAsync();
        if (usersExist != allUserIds.Count)
            throw new InvalidOperationException("Un ou plusieurs utilisateurs sont introuvables ou inactifs.");

        var conversation = new Conversation
        {
            Title = type == ConversationType.Group ? request.Title : null,
            Type = type,
            CreatedById = creatorId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync();

        foreach (var userId in allUserIds)
        {
            _db.ConversationParticipants.Add(new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = userId,
                JoinedAt = DateTime.UtcNow,
                IsGroupAdmin = userId == creatorId && type == ConversationType.Group
            });
        }

        await _db.SaveChangesAsync();

        return await GetConversationByIdAsync(conversation.Id, creatorId)
            ?? throw new InvalidOperationException("Impossible de récupérer la conversation créée.");
    }

    public async Task<bool> AddParticipantAsync(int conversationId, int currentUserId, AddParticipantRequest request)
    {
        var conversation = await _db.Conversations
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null)
            return false;

        var currentParticipant = conversation.Participants.FirstOrDefault(p => p.UserId == currentUserId);
        if (currentParticipant == null)
            return false;

        if (conversation.Type == ConversationType.Group && !currentParticipant.IsGroupAdmin)
            return false;

        var alreadyParticipant = conversation.Participants.Any(p => p.UserId == request.UserId);
        if (alreadyParticipant)
            return false;

        var userExists = await _db.Users.AnyAsync(u => u.Id == request.UserId && u.IsActive);
        if (!userExists)
            return false;

        _db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conversationId,
            UserId = request.UserId,
            JoinedAt = DateTime.UtcNow,
            IsGroupAdmin = request.IsGroupAdmin
        });

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveParticipantAsync(int conversationId, int currentUserId, int targetUserId)
    {
        var conversation = await _db.Conversations
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null)
            return false;

        var currentParticipant = conversation.Participants.FirstOrDefault(p => p.UserId == currentUserId);
        if (currentParticipant == null)
            return false;

        if (conversation.Type == ConversationType.Group && !currentParticipant.IsGroupAdmin && currentUserId != targetUserId)
            return false;

        var participant = conversation.Participants.FirstOrDefault(p => p.UserId == targetUserId);
        if (participant == null)
            return false;

        var remainingCount = conversation.Participants.Count - 1;
        if (remainingCount < 2)
            return false;

        _db.ConversationParticipants.Remove(participant);
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<bool> IsParticipantAsync(int conversationId, int userId) =>
        _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId);

    public async Task<ReadReceiptDto?> MarkAsReadAsync(int conversationId, int userId)
    {
        var participant = await _db.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId);

        if (participant is null)
            return null;

        var readAt = DateTime.UtcNow;
        participant.LastReadAt = readAt;
        await _db.SaveChangesAsync();

        await _notifications.MarkConversationAsReadAsync(userId, conversationId, readAt);

        return new ReadReceiptDto
        {
            ConversationId = conversationId,
            UserId = userId,
            ReadAt = readAt
        };
    }

    private static ConversationDto MapToDto(Conversation c, int currentUserId)
    {
        var currentParticipant = c.Participants.FirstOrDefault(p => p.UserId == currentUserId);
        var lastReadAt = currentParticipant?.LastReadAt ?? currentParticipant?.JoinedAt ?? DateTime.MinValue;

        return new ConversationDto
        {
            Id = c.Id,
            Title = c.Title,
            Type = c.Type.ToString(),
            CreatedById = c.CreatedById,
            CreatedByUsername = c.Participants
                .FirstOrDefault(p => p.UserId == c.CreatedById)?.User.Username ?? string.Empty,
            CreatedAt = c.CreatedAt,
            Participants = c.Participants.Select(p => new ConversationParticipantDto
            {
                Id = p.Id,
                UserId = p.UserId,
                Username = p.User.Username,
                DisplayName = p.User.DisplayName,
                JoinedAt = p.JoinedAt,
                LastReadAt = p.LastReadAt,
                IsGroupAdmin = p.IsGroupAdmin
            }).ToList(),
            LastMessageAt = c.Messages
                .Where(m => !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => (DateTime?)m.CreatedAt)
                .FirstOrDefault(),
            LastMessagePreview = c.Messages
                .Where(m => !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => string.IsNullOrWhiteSpace(m.Content) && m.Attachments.Count > 0
                    ? "📎 " + m.Attachments.OrderBy(a => a.Id).First().OriginalFileName
                    : m.Content)
                .FirstOrDefault(),
            UnreadCount = c.Messages.Count(m =>
                !m.IsDeleted &&
                m.SenderId != currentUserId &&
                m.CreatedAt > lastReadAt)
        };
    }
}
