using Microsoft.Extensions.DependencyInjection;
using OrgChat.Core.Constants;
using OrgChat.Core.Contracts.Auth;
using OrgChat.Core.Contracts.Conversations;
using OrgChat.Core.Contracts.Messages;
using OrgChat.Core.Contracts.Notifications;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Contracts.Users;
using OrgChat.Core.Enums;
using OrgChat.Infrastructure.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace OrgChat.Tests;

public class FeatureIntegrationTests : IClassFixture<OrgChatTestFactory>
{
    private readonly OrgChatTestFactory _factory;

    public FeatureIntegrationTests(OrgChatTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Message_CanBeEditedAndDeleted_OnlyByItsAuthor()
    {
        var suffix = NewSuffix();
        var alice = await Register($"lifealice{suffix}", "Alice Lifecycle");
        var bob = await Register($"lifebob{suffix}", "Bob Lifecycle");

        using var aliceClient = CreateClient(alice.Token);
        using var bobClient = CreateClient(bob.Token);

        var conversation = await CreateConversationAsync(aliceClient, "Private", new[] { bob.UserId });

        var sendResponse = await aliceClient.PostAsJsonAsync(
            $"/api/conversations/{conversation.Id}/messages",
            new MessageCreateRequest { Content = "Version initiale" });
        sendResponse.EnsureSuccessStatusCode();
        var message = (await sendResponse.Content.ReadFromJsonAsync<MessageDto>())!;

        var forbiddenUpdate = await bobClient.PutAsJsonAsync(
            $"/api/conversations/{conversation.Id}/messages/{message.Id}",
            new MessageUpdateRequest { Content = "Modification interdite" });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenUpdate.StatusCode);

        var forbiddenDelete = await bobClient.DeleteAsync(
            $"/api/conversations/{conversation.Id}/messages/{message.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);

        var updateResponse = await aliceClient.PutAsJsonAsync(
            $"/api/conversations/{conversation.Id}/messages/{message.Id}",
            new MessageUpdateRequest { Content = "Version modifiée" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<MessageDto>())!;
        Assert.Equal("Version modifiée", updated.Content);
        Assert.NotNull(updated.EditedAt);

        var deleteResponse = await aliceClient.DeleteAsync(
            $"/api/conversations/{conversation.Id}/messages/{message.Id}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var page = await aliceClient.GetFromJsonAsync<PagedMessageResponse>(
            $"/api/conversations/{conversation.Id}/messages?page=1&pageSize=50");
        Assert.NotNull(page);
        Assert.DoesNotContain(page!.Items, item => item.Id == message.Id);
        Assert.Equal(0, page.TotalCount);

        var notifications = await bobClient.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        Assert.DoesNotContain(notifications!, notification => notification.MessageId == message.Id);
    }

    [Fact]
    public async Task Group_LeaveTransfersAdmin_AndLastDepartureDeletesConversation()
    {
        var suffix = NewSuffix();
        var alice = await Register($"groupalice{suffix}", "Alice Group");
        var bob = await Register($"groupbob{suffix}", "Bob Group");
        var charlie = await Register($"groupcharlie{suffix}", "Charlie Group");

        using var aliceClient = CreateClient(alice.Token);
        using var bobClient = CreateClient(bob.Token);
        using var charlieClient = CreateClient(charlie.Token);

        var group = await CreateConversationAsync(
            aliceClient,
            "Group",
            new[] { bob.UserId, charlie.UserId },
            "Groupe cycle de vie");

        var leaveResponse = await aliceClient.PostAsync(
            $"/api/conversations/{group.Id}/leave", null);
        Assert.Equal(HttpStatusCode.NoContent, leaveResponse.StatusCode);

        var afterLeaveForAlice = await aliceClient.GetAsync($"/api/conversations/{group.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterLeaveForAlice.StatusCode);

        var afterLeaveForBob = await bobClient.GetFromJsonAsync<ConversationDto>(
            $"/api/conversations/{group.Id}");
        Assert.NotNull(afterLeaveForBob);
        Assert.Equal(2, afterLeaveForBob!.Participants.Count);
        Assert.Single(afterLeaveForBob.Participants, participant => participant.IsGroupAdmin);

        var leaveByBob = await bobClient.PostAsync($"/api/conversations/{group.Id}/leave", null);
        Assert.Equal(HttpStatusCode.NoContent, leaveByBob.StatusCode);

        var afterLastLeave = await charlieClient.GetAsync($"/api/conversations/{group.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterLastLeave.StatusCode);

        var privateConversation = await CreateConversationAsync(
            aliceClient, "Private", new[] { bob.UserId });

        var deletePrivate = await bobClient.DeleteAsync(
            $"/api/conversations/{privateConversation.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deletePrivate.StatusCode);

        var privateAfterDelete = await aliceClient.GetAsync(
            $"/api/conversations/{privateConversation.Id}");
        Assert.Equal(HttpStatusCode.NotFound, privateAfterDelete.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCreatePromoteResetAndDeleteAccount()
    {
        var suffix = NewSuffix();
        var admin = await Register($"admin{suffix}", "Admin Test");
        await PromoteToAdminAsync(admin.UserId);

        using var adminClient = CreateClient((await LoginAsync(admin.Username, "Password123!")).Token);
        using var regularClient = CreateClient(admin.Token);

        var username = $"managed{suffix}";
        var email = $"managed{suffix}@example.org";
        var createdResponse = await regularClient.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Username = username,
            Email = email,
            DisplayName = "Compte géré",
            Password = "Password123!",
            Role = "User"
        });
        Assert.Equal(HttpStatusCode.Forbidden, createdResponse.StatusCode);

        createdResponse = await adminClient.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Username = username,
            Email = email,
            DisplayName = "Compte géré",
            Password = "Password123!",
            Role = "User"
        });
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal("User", created.Role);
        Assert.True(created.IsActive);

        var roleResponse = await adminClient.PutAsJsonAsync(
            $"/api/users/{created.Id}/role",
            new UpdateUserRoleRequest { Role = RoleKeys.OrgAdmin });
        Assert.Equal(HttpStatusCode.OK, roleResponse.StatusCode);
        var promoted = (await roleResponse.Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal(RoleKeys.OrgAdmin, promoted.Role);

        var newPassword = "NouveauPass123!";
        var resetResponse = await adminClient.PutAsJsonAsync(
            $"/api/users/{created.Id}/password",
            new ResetPasswordRequest { NewPassword = newPassword });
        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

        var loginAfterReset = await LoginAsync(username, newPassword);
        Assert.Equal(created.Id, loginAfterReset.UserId);

        using var managedClient = CreateClient(loginAfterReset.Token);
        var activeMe = await managedClient.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, activeMe.StatusCode);

        var selfDelete = await adminClient.DeleteAsync($"/api/users/{admin.UserId}");
        Assert.Equal(HttpStatusCode.BadRequest, selfDelete.StatusCode);

        var selfDemotion = await adminClient.PutAsJsonAsync(
            $"/api/users/{admin.UserId}/role",
            new UpdateUserRoleRequest { Role = "User" });
        Assert.Equal(HttpStatusCode.BadRequest, selfDemotion.StatusCode);

        var deleteResponse = await adminClient.DeleteAsync($"/api/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var deletedMe = await managedClient.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, deletedMe.StatusCode);

        var anonymized = await adminClient.GetFromJsonAsync<UserDto>($"/api/users/{created.Id}");
        Assert.NotNull(anonymized);
        Assert.False(anonymized!.IsActive);
        Assert.StartsWith("deleted_", anonymized.Username);
        Assert.Equal("Compte supprimé", anonymized.DisplayName);
    }

    [Fact]
    public async Task Search_ReturnsOnlyAccessibleNonDeletedMessages()
    {
        var suffix = NewSuffix();
        var token = $"recherche{suffix}";
        var alice = await Register($"searchalice{suffix}", "Alice Search");
        var bob = await Register($"searchbob{suffix}", "Bob Search");
        var charlie = await Register($"searchcharlie{suffix}", "Charlie Search");
        var dave = await Register($"searchdave{suffix}", "Dave Search");

        using var aliceClient = CreateClient(alice.Token);
        using var bobClient = CreateClient(bob.Token);
        using var charlieClient = CreateClient(charlie.Token);

        var aliceConversation = await CreateConversationAsync(aliceClient, "Private", new[] { bob.UserId });
        var charlieConversation = await CreateConversationAsync(charlieClient, "Private", new[] { dave.UserId });

        var aliceMessageResponse = await aliceClient.PostAsJsonAsync(
            $"/api/conversations/{aliceConversation.Id}/messages",
            new MessageCreateRequest { Content = $"Le projet {token} avance bien." });
        aliceMessageResponse.EnsureSuccessStatusCode();
        var aliceMessage = (await aliceMessageResponse.Content.ReadFromJsonAsync<MessageDto>())!;

        var charlieMessageResponse = await charlieClient.PostAsJsonAsync(
            $"/api/conversations/{charlieConversation.Id}/messages",
            new MessageCreateRequest { Content = $"Le projet {token} est confidentiel." });
        charlieMessageResponse.EnsureSuccessStatusCode();

        var aliceResults = await aliceClient.GetFromJsonAsync<List<MessageSearchResultDto>>(
            $"/api/search/messages?q={token}");
        Assert.NotNull(aliceResults);
        var aliceResult = Assert.Single(aliceResults!);
        Assert.Equal(aliceConversation.Id, aliceResult.ConversationId);
        Assert.Equal(aliceMessage.Id, aliceResult.MessageId);

        var bobResults = await bobClient.GetFromJsonAsync<List<MessageSearchResultDto>>(
            $"/api/search/messages?q={token}");
        Assert.Single(bobResults!);

        var charlieResults = await charlieClient.GetFromJsonAsync<List<MessageSearchResultDto>>(
            $"/api/search/messages?q={token}");
        Assert.Single(charlieResults!);

        var tooShort = await aliceClient.GetFromJsonAsync<List<MessageSearchResultDto>>(
            "/api/search/messages?q=a");
        Assert.Empty(tooShort!);

        var deleteResponse = await aliceClient.DeleteAsync(
            $"/api/conversations/{aliceConversation.Id}/messages/{aliceMessage.Id}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var afterDelete = await aliceClient.GetFromJsonAsync<List<MessageSearchResultDto>>(
            $"/api/search/messages?q={token}");
        Assert.Empty(afterDelete!);
    }

    [Fact]
    public async Task Notifications_SupportPaginationDeletionAndClear()
    {
        var suffix = NewSuffix();
        var alice = await Register($"notifpagealice{suffix}", "Alice Notif Page");
        var bob = await Register($"notifpagebob{suffix}", "Bob Notif Page");

        using var aliceClient = CreateClient(alice.Token);
        using var bobClient = CreateClient(bob.Token);

        var conversation = await CreateConversationAsync(aliceClient, "Private", new[] { bob.UserId });

        for (var index = 1; index <= 3; index++)
        {
            var send = await bobClient.PostAsJsonAsync(
                $"/api/conversations/{conversation.Id}/messages",
                new MessageCreateRequest { Content = $"Notification {index}" });
            send.EnsureSuccessStatusCode();
        }

        var firstPage = await aliceClient.GetFromJsonAsync<List<NotificationDto>>(
            "/api/notifications?take=2&skip=0");
        Assert.Equal(2, firstPage!.Count);

        var secondPage = await aliceClient.GetFromJsonAsync<List<NotificationDto>>(
            "/api/notifications?take=2&skip=2");
        Assert.Single(secondPage!);

        var stolenDelete = await bobClient.DeleteAsync($"/api/notifications/{firstPage[0].Id}");
        Assert.Equal(HttpStatusCode.NotFound, stolenDelete.StatusCode);

        var deleteResponse = await aliceClient.DeleteAsync($"/api/notifications/{firstPage[0].Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterDelete = await aliceClient.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        Assert.Equal(2, afterDelete!.Count);
        Assert.DoesNotContain(afterDelete, notification => notification.Id == firstPage[0].Id);

        var clearResponse = await aliceClient.DeleteAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);

        var afterClear = await aliceClient.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        Assert.Empty(afterClear!);

        var count = await aliceClient.GetFromJsonAsync<NotificationCountDto>(
            "/api/notifications/unread-count");
        Assert.Equal(0, count!.UnreadCount);
    }

    private static string NewSuffix() => Guid.NewGuid().ToString("N")[..8];

    private async Task<AuthResponse> Register(string username, string displayName)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username,
            Email = $"{username}@example.org",
            DisplayName = displayName,
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

    private HttpClient CreateClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<ConversationDto> CreateConversationAsync(
        HttpClient client,
        string type,
        IEnumerable<int> participantIds,
        string? title = null)
    {
        var response = await client.PostAsJsonAsync("/api/conversations", new ConversationCreateRequest
        {
            Type = type,
            Title = title,
            ParticipantUserIds = participantIds.ToList()
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConversationDto>())!;
    }

    private async Task PromoteToAdminAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var userRoleService = scope.ServiceProvider.GetRequiredService<IUserRoleService>();
        var user = await userRoleService.GetUserWithRolesAsync(userId)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");
        await userRoleService.SetOrganizationRoleAsync(user, RoleKeys.OrgAdmin);
    }
}
