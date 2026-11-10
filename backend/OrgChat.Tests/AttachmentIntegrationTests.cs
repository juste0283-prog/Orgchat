using OrgChat.Core.Contracts.Auth;
using OrgChat.Core.Contracts.Conversations;
using OrgChat.Core.Contracts.Messages;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Xunit;

namespace OrgChat.Tests;

public class AttachmentIntegrationTests : IClassFixture<OrgChatTestFactory>
{
    private readonly OrgChatTestFactory _factory;

    public AttachmentIntegrationTests(OrgChatTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UploadAndDownloadAttachment_WorksForParticipantOnly()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var alice = await Register($"attachalice{suffix}", $"attachalice{suffix}@example.org", "Alice Attach");
        var bob = await Register($"attachbob{suffix}", $"attachbob{suffix}@example.org", "Bob Attach");
        var outsider = await Register($"attachout{suffix}", $"attachout{suffix}@example.org", "Outsider");

        using var aliceClient = CreateClient(alice.Token);
        using var outsiderClient = CreateClient(outsider.Token);

        var conversation = await CreateConversation(aliceClient, bob.UserId);
        var fileBytes = Encoding.UTF8.GetBytes("Contenu de la pièce jointe.");

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(fileBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "notes.txt");
        form.Add(new StringContent("Légende du fichier", Encoding.UTF8), "content");

        var uploadResponse = await aliceClient.PostAsync(
            $"/api/conversations/{conversation.Id}/messages/attachments", form);
        uploadResponse.EnsureSuccessStatusCode();

        var message = await uploadResponse.Content.ReadFromJsonAsync<MessageDto>();
        Assert.NotNull(message);
        Assert.Equal("Légende du fichier", message.Content);
        var attachment = Assert.Single(message.Attachments);
        Assert.Equal("notes.txt", attachment.OriginalFileName);
        Assert.Equal("text/plain", attachment.ContentType);
        Assert.Equal(fileBytes.Length, attachment.SizeInBytes);
        Assert.Equal("/api/attachments/" + attachment.Id, attachment.DownloadUrl);

        var downloadResponse = await aliceClient.GetAsync($"/api/attachments/{attachment.Id}");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal(fileBytes, await downloadResponse.Content.ReadAsByteArrayAsync());

        var forbiddenResponse = await outsiderClient.GetAsync($"/api/attachments/{attachment.Id}");
        Assert.Equal(HttpStatusCode.NotFound, forbiddenResponse.StatusCode);

        var history = await aliceClient.GetFromJsonAsync<PagedMessageResponse>(
            $"/api/conversations/{conversation.Id}/messages");
        var stored = Assert.Single(history!.Items, m => m.Id == message.Id);
        Assert.Single(stored.Attachments);
    }

    [Fact]
    public async Task UploadAttachment_RejectsBlockedExtension()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var alice = await Register($"blockalice{suffix}", $"blockalice{suffix}@example.org", "Alice Block");
        var bob = await Register($"blockbob{suffix}", $"blockbob{suffix}@example.org", "Bob Block");

        using var aliceClient = CreateClient(alice.Token);
        var conversation = await CreateConversation(aliceClient, bob.UserId);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("MZ"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", "virus.exe");

        var response = await aliceClient.PostAsync(
            $"/api/conversations/{conversation.Id}/messages/attachments", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
