using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrgChat.Core.Constants;
using OrgChat.Core.Contracts.Auth;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Contracts.Users;
using OrgChat.Infrastructure.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace OrgChat.Tests;

/// <summary>
/// Tests du flux §1 : création centralisée avec invitation, activation,
/// suspension, sessions et historique.
/// </summary>
public class UserActivationIntegrationTests : IClassFixture<OrgChatTestFactory>
{
    private readonly OrgChatTestFactory _factory;

    public UserActivationIntegrationTests(OrgChatTestFactory factory)
    {
        _factory = factory;
    }

    private static string NewSuffix() => Guid.NewGuid().ToString("N")[..8];

    private HttpClient NewClient() => _factory.CreateClient();

    private static HttpClient WithToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<AuthResponse> RegisterAsync(string username)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username,
            Email = $"{username}@example.org",
            DisplayName = username,
            Password = "Password123!"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task<AuthResponse> LoginAsync(string username, string password)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task PromoteToRoleAsync(int userId, string roleKey)
    {
        using var scope = _factory.Services.CreateScope();
        var roleService = scope.ServiceProvider.GetRequiredService<IUserRoleService>();
        var user = await roleService.GetUserWithRolesAsync(userId)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");
        await roleService.SetOrganizationRoleAsync(user, roleKey);
    }

    [Fact]
    public async Task Admin_CreatesUser_WithActivationLink_And_UserActivates()
    {
        var suffix = NewSuffix();
        var adminIdentity = await RegisterAsync($"actadmin{suffix}");
        await PromoteToRoleAsync(adminIdentity.UserId, RoleKeys.OrgAdmin);
        var adminAuth = await LoginAsync(adminIdentity.Username, "Password123!");
        using var adminClient = WithToken(NewClient(), adminAuth.Token);

        var createdResponse = await adminClient.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            FirstName = "Moussa",
            LastName = "Diop",
            Email = $"moussa{suffix}@exemple.org",
            Role = RoleKeys.User,
            JobTitle = "Développeur",
        });
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<CreatedUserDto>())!;

        Assert.False(string.IsNullOrWhiteSpace(created.Username));
        Assert.Equal("moussa.diop", created.Username);
        Assert.False(string.IsNullOrWhiteSpace(created.ActivationToken));
        Assert.False(string.IsNullOrWhiteSpace(created.ActivationLink));
        Assert.NotNull(created.InvitationExpiresAt);
        Assert.True(created.MustChangePassword);

        // Le compte ne peut pas se connecter avant activation.
        using var anon = NewClient();
        var earlyLogin = await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = created.Username,
            Password = "Choisi123!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, earlyLogin.StatusCode);

        // Activation première connexion.
        var activateResponse = await anon.PostAsJsonAsync("/api/auth/activate", new ActivateAccountRequest
        {
            Token = created.ActivationToken!,
            NewPassword = "Choisi123!"
        });
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        var activated = (await activateResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.Equal(created.Id, activated.UserId);

        // Le jeton est à usage unique : réutilisation refusée.
        var reuseResponse = await anon.PostAsJsonAsync("/api/auth/activate", new ActivateAccountRequest
        {
            Token = created.ActivationToken!,
            NewPassword = "Autre123!"
        });
        Assert.Equal(HttpStatusCode.BadRequest, reuseResponse.StatusCode);

        // Connexion classique après activation, puis activité journalisée.
        var login = await LoginAsync(created.Username, "Choisi123!");
        using var userClient = WithToken(NewClient(), login.Token);
        var activity = await userClient.GetFromJsonAsync<List<ActivityItemDto>>("/api/users/me/activity");
        Assert.NotNull(activity);
        Assert.Contains(activity!, a => a.Type == "Login" || a.Type == "AccountActivated");
    }

    [Fact]
    public async Task OrgAdmin_Cannot_Designate_Supervisor()
    {
        var suffix = NewSuffix();
        var adminIdentity = await RegisterAsync($"noSup{suffix}");
        await PromoteToRoleAsync(adminIdentity.UserId, RoleKeys.OrgAdmin);
        var adminAuth = await LoginAsync(adminIdentity.Username, "Password123!");
        using var adminClient = WithToken(NewClient(), adminAuth.Token);

        var response = await adminClient.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            FirstName = "Awa",
            LastName = "Fall",
            Email = $"awa{suffix}@exemple.org",
            Role = RoleKeys.Supervisor,
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Suspended_User_Cannot_Login_Then_Can_After_Unsuspend()
    {
        var suffix = NewSuffix();
        var adminIdentity = await RegisterAsync($"suspadmin{suffix}");
        await PromoteToRoleAsync(adminIdentity.UserId, RoleKeys.OrgAdmin);
        var adminAuth = await LoginAsync(adminIdentity.Username, "Password123!");
        using var adminClient = WithToken(NewClient(), adminAuth.Token);

        var createdResponse = await adminClient.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Username = $"susp{suffix}",
            Email = $"susp{suffix}@exemple.org",
            DisplayName = "Compte suspendu",
            Password = "Password123!",
            Role = RoleKeys.User,
        });
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<UserDto>())!;

        var suspendResponse = await adminClient.PostAsJsonAsync(
            $"/api/users/{created.Id}/suspend",
            new SuspendUserRequest { DurationDays = 1, Reason = "Test" });
        Assert.Equal(HttpStatusCode.NoContent, suspendResponse.StatusCode);

        using var anon = NewClient();
        var suspendedLogin = await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = created.Username,
            Password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, suspendedLogin.StatusCode);

        var unsuspendResponse = await adminClient.PostAsJsonAsync<object?>($"/api/users/{created.Id}/unsuspend", null);
        Assert.Equal(HttpStatusCode.NoContent, unsuspendResponse.StatusCode);

        var login = await LoginAsync(created.Username, "Password123!");
        Assert.Equal(created.Id, login.UserId);
    }
}
