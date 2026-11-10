using OrgChat.Core.Contracts.Auth;
using OrgChat.Core.Contracts.Conversations;
using OrgChat.Core.Contracts.Messages;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace OrgChat.Tests;

public class MessageIntegrationTests : IClassFixture<OrgChatTestFactory>
{
    private readonly HttpClient _client;
    private string _aliceToken;
    private string _bobToken;
    private int _conversationId;
    private readonly OrgChatTestFactory _factory;

    public MessageIntegrationTests(OrgChatTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        SetupAsync().GetAwaiter().GetResult();
    }

    private async Task SetupAsync()
    {
        // Créer deux utilisateurs
        _aliceToken = await RegisterAndGetToken("alice", "alice@example.org", "Alice");
        _bobToken = await RegisterAndGetToken("bob", "bob@example.org", "Bob");

        // Créer une conversation privée entre alice et bob
        var aliceClient = _factory.CreateClient();
        aliceClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _aliceToken);

        var createRequest = new ConversationCreateRequest
        {
            Type = "Private",
            ParticipantUserIds = new List<int> { 2 } // bob
        };
        var createResponse = await aliceClient.PostAsJsonAsync("/api/conversations", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ConversationDto>();
        _conversationId = created!.Id;
    }

    private async Task<string> RegisterAndGetToken(string username, string email, string displayName)
    {
        var request = new RegisterRequest
        {
            Username = username,
            Email = email,
            DisplayName = displayName,
            Password = "Password123!"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            // Le fixture partage la base entre les tests de la classe : réutiliser le compte existant.
            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Username = username,
                Password = "Password123!"
            });
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
            return loginResult!.Token;
        }

        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token;
    }

    private void SetToken(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task SendMessage_Success()
    {
        SetToken(_aliceToken);

        var request = new MessageCreateRequest { Content = "Bonjour Bob !" };

        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{_conversationId}/messages", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MessageDto>();
        Assert.NotNull(result);
        Assert.Equal("Bonjour Bob !", result.Content);
        Assert.Equal(1, result.SenderId); // alice
    }

    [Fact]
    public async Task GetMessages_Paginated()
    {
        SetToken(_aliceToken);

        // Envoyer plusieurs messages
        for (int i = 1; i <= 5; i++)
        {
            await _client.PostAsJsonAsync(
                $"/api/conversations/{_conversationId}/messages",
                new MessageCreateRequest { Content = $"Message {i}" });
        }

        var response = await _client.GetAsync(
            $"/api/conversations/{_conversationId}/messages?page=1&pageSize=3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedMessageResponse>();
        Assert.NotNull(result);
        Assert.Equal(3, result.Items.Count);
        Assert.True(result.HasNextPage);
        Assert.Equal(5, result.TotalCount);

        var nextPageResponse = await _client.GetAsync(
            $"/api/conversations/{_conversationId}/messages?page=2&pageSize=3");
        Assert.Equal(HttpStatusCode.OK, nextPageResponse.StatusCode);
        var nextPage = await nextPageResponse.Content.ReadFromJsonAsync<PagedMessageResponse>();
        Assert.NotNull(nextPage);
        Assert.Equal(2, nextPage.Items.Count);
        Assert.Empty(result.Items.Select(message => message.Id)
            .Intersect(nextPage.Items.Select(message => message.Id)));
    }

    [Fact]
    public async Task SendMessage_NotParticipant_ReturnsBadRequest()
    {
        // Créer un troisième utilisateur qui n'est pas dans la conversation
        var charlieToken = await RegisterAndGetToken("charlie", "charlie@example.org", "Charlie");
        SetToken(charlieToken);

        var request = new MessageCreateRequest { Content = "Je ne suis pas participant" };

        var response = await _client.PostAsJsonAsync(
            $"/api/conversations/{_conversationId}/messages", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
