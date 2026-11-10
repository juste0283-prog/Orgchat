using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Entities;

namespace OrgChat.Infrastructure.Data;

public static class SqliteDatabaseTestData
{
    private const string DemoConversationTitle = "DEMO - Conversation de test";
    private const string DemoMessageContent = "Message de démonstration OrgChat.";

    public static async Task SeedDemoDataAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var alice = await db.Users.SingleOrDefaultAsync(
            user => user.Username == "demo-alice",
            cancellationToken);
        if (alice is null)
        {
            alice = new User
            {
                Username = "demo-alice",
                Email = "demo-alice@example.invalid",
                DisplayName = "Alice de démonstration",
                PasswordHash = "TEST_ONLY_INVALID_PASSWORD_HASH"
            };
            db.Users.Add(alice);
        }

        var bob = await db.Users.SingleOrDefaultAsync(
            user => user.Username == "demo-bob",
            cancellationToken);
        if (bob is null)
        {
            bob = new User
            {
                Username = "demo-bob",
                Email = "demo-bob@example.invalid",
                DisplayName = "Bob de démonstration",
                PasswordHash = "TEST_ONLY_INVALID_PASSWORD_HASH"
            };
            db.Users.Add(bob);
        }

        await db.SaveChangesAsync(cancellationToken);

        var conversation = await db.Conversations.SingleOrDefaultAsync(
            item => item.Title == DemoConversationTitle && item.CreatedById == alice.Id,
            cancellationToken);
        if (conversation is null)
        {
            conversation = new Conversation
            {
                Title = DemoConversationTitle,
                Type = OrgChat.Core.Enums.ConversationType.Group,
                CreatedById = alice.Id
            };
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync(cancellationToken);
        }

        await AddParticipantIfMissingAsync(
            db, conversation.Id, alice.Id, isGroupAdmin: true, cancellationToken: cancellationToken);
        await AddParticipantIfMissingAsync(
            db, conversation.Id, bob.Id, isGroupAdmin: false, cancellationToken: cancellationToken);

        var messageExists = await db.Messages.AnyAsync(
            message => message.ConversationId == conversation.Id
                       && message.SenderId == alice.Id
                       && message.Content == DemoMessageContent,
            cancellationToken);
        if (!messageExists)
        {
            db.Messages.Add(new Message
            {
                ConversationId = conversation.Id,
                SenderId = alice.Id,
                Content = DemoMessageContent
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task AddParticipantIfMissingAsync(
        AppDbContext db,
        int conversationId,
        int userId,
        bool isGroupAdmin,
        CancellationToken cancellationToken)
    {
        var exists = await db.ConversationParticipants.AnyAsync(
            participant => participant.ConversationId == conversationId
                           && participant.UserId == userId,
            cancellationToken);
        if (exists)
            return;

        db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conversationId,
            UserId = userId,
            IsGroupAdmin = isGroupAdmin
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
