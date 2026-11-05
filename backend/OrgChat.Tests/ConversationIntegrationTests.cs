using OrgChat.Core.Contracts.Auth;
using OrgChat.Core.Contracts.Conversations;
using OrgChat.Core.Contracts.Messages;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace OrgChat.Tests;

public class ConversationIntegrationTests : IClassFixture<OrgChatTestFactory>
{
    private readonly HttpClient _client;
    private readonly string _aliceToken;
    private readonly string _bobToken;

    public ConversationIntegrationTests(OrgChatTestFactory factory)
    {
        _client = factory.CreateClient();

        // Créer deux utilisateurs et récupérer leurs tokens
        _aliceToken = RegisterAndGetToken("alice", "alice@example.org", "Alice").Result;
        _bobToken = RegisterAndGetToken("bob", "bob@example.org", "Bob").Result;
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
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return result!.Token;
    }

    private void SetToken(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task CreatePrivateConversation_Success()
    {
        SetToken(_aliceToken);

        var request = new ConversationCreateRequest
        {
            Type = "Private",
            ParticipantUserIds = new List<int> { 2 } // bob
        };

        var response = await _client.PostAsJsonAsync("/api/conversations", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ConversationDto>();
        Assert.NotNull(result);
        Assert.Equal("Private", result.Type);
        Assert.Equal(2, result.Participants.Count);
    }

    [Fact]
    public async Task CreateGroupConversation_Success()
    {
        SetToken(_aliceToken);

        var request = new ConversationCreateRequest
        {
            Title = "Groupe de projet",
            Type = "Group",
            ParticipantUserIds = new List<int> { 2 } // bob
        };

        var response = await _client.PostAsJsonAsync("/api/conversations", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ConversationDto>();
        Assert.NotNull(result);
        Assert.Equal("Group", result.Type);
        Assert.Equal("Groupe de projet", result.Title);
    }

    [Fact]
    public async Task GetConversations_ReturnsUserConversations()
    {
        SetToken(_aliceToken);

        // Créer une conversation privée
        var createRequest = new ConversationCreateRequest
        {
            Type = "Private",
            ParticipantUserIds = new List<int> { 2 }
        };
        await _client.PostAsJsonAsync("/api/conversations", createRequest);

        var response = await _client.GetAsync("/api/conversations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<ConversationDto>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task GetConversation_WithoutAccess_ReturnsNotFound()
    {
        SetToken(_aliceToken);

        // Créer une conversation entre alice et bob
        var createRequest = new ConversationCreateRequest
        {
            Type = "Private",
            ParticipantUserIds = new List<int> { 2 }
        };
        var createResponse = await _client.PostAsJsonAsync("/api/conversations", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<ConversationDto>();

        // Essayer d'y accéder avec un autre utilisateur (il faut en créer un troisième)
        var charlieToken = RegisterAndGetToken("charlie", "charlie@example.org", "Charlie").Result;
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", charlieToken);

        var response = await _client.GetAsync($"/api/conversations/{created!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
