using OrgChat.Core.Contracts.Auth;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace OrgChat.Tests;

public class AuthIntegrationTests : IClassFixture<OrgChatTestFactory>
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(OrgChatTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidRequest_ReturnsToken()
    {
        var request = new RegisterRequest
        {
            Username = "alice",
            Email = "alice@example.org",
            DisplayName = "Alice",
            Password = "Password123!"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.Equal("alice", result.Username);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsBadRequest()
    {
        var request = new RegisterRequest
        {
            Username = "bob",
            Email = "bob@example.org",
            DisplayName = "Bob",
            Password = "Password123!"
        };

        await _client.PostAsJsonAsync("/api/auth/register", request);
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var register = new RegisterRequest
        {
            Username = "charlie",
            Email = "charlie@example.org",
            DisplayName = "Charlie",
            Password = "Password123!"
        };
        await _client.PostAsJsonAsync("/api/auth/register", register);

        var login = new LoginRequest
        {
            Username = "charlie",
            Password = "Password123!"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", login);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.Token));
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var register = new RegisterRequest
        {
            Username = "dave",
            Email = "dave@example.org",
            DisplayName = "Dave",
            Password = "Password123!"
        };
        await _client.PostAsJsonAsync("/api/auth/register", register);

        var login = new LoginRequest
        {
            Username = "dave",
            Password = "WrongPassword!"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/login", login);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
