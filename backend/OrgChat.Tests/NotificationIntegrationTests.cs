using OrgChat.Core.Contracts.Auth;
using OrgChat.Core.Contracts.Conversations;
using OrgChat.Core.Contracts.Messages;
using OrgChat.Core.Contracts.Notifications;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace OrgChat.Tests;

public class NotificationIntegrationTests : IClassFixture<OrgChatTestFactory>
{
    private readonly OrgChatTestFactory _factory;

    public NotificationIntegrationTests(OrgChatTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Message_CreatesPersistedNotification_AndReadFlowUpdatesCount()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var alice = await Register($"notifalice{suffix}", $"notifalice{suffix}@example.org", "Alice Notif");
        var bob = await Register($"notifbob{suffix}", $"notifbob{suffix}@example.org", "Bob Notif");

        using var aliceClient = CreateClient(alice.Token);
        using var bobClient = CreateClient(bob.Token);

        var conversation = await CreateConversation(aliceClient, bob.UserId);

        var messageResponse = await bobClient.PostAsJsonAsync(
            $"/api/conversations/{conversation.Id}/messages",
            new MessageCreateRequest { Content = "Message à notifier" });
        messageResponse.EnsureSuccessStatusCode();
        var message = await messageResponse.Content.ReadFromJsonAsync<MessageDto>();

        var notifications = await aliceClient.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        Assert.NotNull(notifications);
        var notification = Assert.Single(notifications!, n => n.MessageId == message!.Id);
        Assert.Equal(alice.UserId, notification.UserId);
        Assert.Equal(conversation.Id, notification.ConversationId);
        Assert.Equal(bob.UserId, notification.SenderId);
        Assert.Equal("Bob Notif", notification.SenderName);
        Assert.Equal("Message à notifier", notification.Preview);
        Assert.False(notification.IsRead);

        var count = await aliceClient.GetFromJsonAsync<NotificationCountDto>("/api/notifications/unread-count");
        Assert.Equal(1, count!.UnreadCount);

        var readResponse = await aliceClient.PostAsync($"/api/notifications/{notification.Id}/read", null);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        var read = await readResponse.Content.ReadFromJsonAsync<NotificationDto>();
        Assert.True(read!.IsRead);

        var afterReadCount = await aliceClient.GetFromJsonAsync<NotificationCountDto>(
            "/api/notifications/unread-count");
        Assert.Equal(0, afterReadCount!.UnreadCount);

        var bobNotifications = await bobClient.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        Assert.DoesNotContain(bobNotifications!, n => n.MessageId == message!.Id);
    }

    [Fact]
    public async Task MarkConversationRead_MarksRelatedNotificationsRead()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var alice = await Register($"convreadalice{suffix}", $"convreadalice{suffix}@example.org", "Alice Conv");
        var bob = await Register($"convreadbob{suffix}", $"convreadbob{suffix}@example.org", "Bob Conv");

        using var aliceClient = CreateClient(alice.Token);
        using var bobClient = CreateClient(bob.Token);

        var conversation = await CreateConversation(aliceClient, bob.UserId);

        var firstResponse = await bobClient.PostAsJsonAsync(
            $"/api/conversations/{conversation.Id}/messages",
            new MessageCreateRequest { Content = "Premier message" });
        firstResponse.EnsureSuccessStatusCode();

        var before = await aliceClient.GetFromJsonAsync<NotificationCountDto>(
            "/api/notifications/unread-count");
        Assert.Equal(1, before!.UnreadCount);

        var readResponse = await aliceClient.PostAsync($"/api/conversations/{conversation.Id}/read", null);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);

        var after = await aliceClient.GetFromJsonAsync<NotificationCountDto>(
            "/api/notifications/unread-count");
        Assert.Equal(0, after!.UnreadCount);

        var notifications = await aliceClient.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        Assert.All(
            notifications!.Where(n => n.ConversationId == conversation.Id),
            n => Assert.True(n.IsRead));

        var secondResponse = await bobClient.PostAsJsonAsync(
            $"/api/conversations/{conversation.Id}/messages",
            new MessageCreateRequest { Content = "Deuxième message" });
        secondResponse.EnsureSuccessStatusCode();

        var markAllResponse = await aliceClient.PostAsync("/api/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.OK, markAllResponse.StatusCode);

        var finalCount = await aliceClient.GetFromJsonAsync<NotificationCountDto>(
            "/api/notifications/unread-count");
        Assert.Equal(0, finalCount!.UnreadCount);
    }

    private async Task<ConversationDto> CreateConversation(HttpClient client, int participantUserId)
    {
        var response = await client.PostAsJsonAsync("/api/conversations", new ConversationCreateRequest
        {
            Type = "Private",
            ParticipantUserIds = new List<int> { participantUserId }
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConversationDto>())!;
    }

    private async Task<AuthResponse> Register(string username, string email, string displayName)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username,
            Email = email,
            DisplayName = displayName,
            Password = "Password123!"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private HttpClient CreateClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
