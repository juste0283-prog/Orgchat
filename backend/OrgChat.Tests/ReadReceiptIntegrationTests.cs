using OrgChat.Core.Contracts.Auth;
using OrgChat.Core.Contracts.Conversations;
using OrgChat.Core.Contracts.Messages;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace OrgChat.Tests;

public class ReadReceiptIntegrationTests : IClassFixture<OrgChatTestFactory>
{
    private readonly OrgChatTestFactory _factory;

    public ReadReceiptIntegrationTests(OrgChatTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MarkAsRead_ResetsUnreadCount()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var alice = await Register($"readalice{suffix}", $"readalice{suffix}@example.org", "Alice Read");
        var bob = await Register($"readbob{suffix}", $"readbob{suffix}@example.org", "Bob Read");

        using var aliceClient = CreateClient(alice.Token);
        using var bobClient = CreateClient(bob.Token);

        var createResponse = await aliceClient.PostAsJsonAsync("/api/conversations", new ConversationCreateRequest
        {
            Type = "Private",
            ParticipantUserIds = new List<int> { bob.UserId }
        });
        createResponse.EnsureSuccessStatusCode();
        var conversation = await createResponse.Content.ReadFromJsonAsync<ConversationDto>();
        Assert.NotNull(conversation);

        var messageResponse = await bobClient.PostAsJsonAsync(
            $"/api/conversations/{conversation.Id}/messages",
            new MessageCreateRequest { Content = "Message non lu" });
        messageResponse.EnsureSuccessStatusCode();

        var beforeRead = await aliceClient.GetFromJsonAsync<List<ConversationDto>>("/api/conversations");
        var aliceConversation = Assert.Single(beforeRead!, c => c.Id == conversation.Id);
        Assert.Equal(1, aliceConversation.UnreadCount);

        var readResponse = await aliceClient.PostAsync($"/api/conversations/{conversation.Id}/read", null);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        var receipt = await readResponse.Content.ReadFromJsonAsync<ReadReceiptDto>();
        Assert.NotNull(receipt);
        Assert.Equal(conversation.Id, receipt.ConversationId);
        Assert.Equal(alice.UserId, receipt.UserId);

        var afterRead = await aliceClient.GetFromJsonAsync<List<ConversationDto>>("/api/conversations");
        var updatedConversation = Assert.Single(afterRead!, c => c.Id == conversation.Id);
        Assert.Equal(0, updatedConversation.UnreadCount);
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
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!;
    }

    private HttpClient CreateClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
