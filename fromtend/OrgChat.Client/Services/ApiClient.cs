using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OrgChat.Client.Models;

namespace OrgChat.Client.Services;

/// <summary>
/// Point d'entrée unique vers l'API REST OrgChat.
/// Toutes les URL sont centralisées ici, jamais dans les formulaires.
/// </summary>
public sealed class ApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private string _baseUrl;
    private string? _token;

    public ApiClient(string baseUrl)
    {
        _baseUrl = Normalize(baseUrl);
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public string BaseUrl => _baseUrl;

    public string? Token => _token;

    public void SetBaseUrl(string baseUrl) => _baseUrl = Normalize(baseUrl);

    public void SetToken(string? token) => _token = token;

    // ------------------------------------------------------------------ Auth

    public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/login", request, auth: false, ct);

    public Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/register", request, auth: false, ct);

    // ----------------------------------------------------------------- Users

    public Task<UserDto> GetMeAsync(CancellationToken ct = default) =>
        SendAsync<UserDto>(HttpMethod.Get, "api/users/me", null, auth: true, ct);

    public Task<List<UserDto>> GetUsersAsync(CancellationToken ct = default) =>
        SendAsync<List<UserDto>>(HttpMethod.Get, "api/users", null, auth: true, ct);

    public Task<UserDto> GetUserAsync(int id, CancellationToken ct = default) =>
        SendAsync<UserDto>(HttpMethod.Get, $"api/users/{id}", null, auth: true, ct);

    public Task<UserDto> UpdateUserAsync(int id, UpdateUserRequest request, CancellationToken ct = default) =>
        SendAsync<UserDto>(HttpMethod.Put, $"api/users/{id}", request, auth: true, ct);

    public Task DeactivateUserAsync(int id, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Put, $"api/users/{id}/deactivate", null, ct);

    public Task ReactivateUserAsync(int id, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Put, $"api/users/{id}/reactivate", null, ct);

    // --------------------------------------------------------- Conversations

    public Task<List<ConversationDto>> GetConversationsAsync(CancellationToken ct = default) =>
        SendAsync<List<ConversationDto>>(HttpMethod.Get, "api/conversations", null, auth: true, ct);

    public Task<ConversationDto> GetConversationAsync(int id, CancellationToken ct = default) =>
        SendAsync<ConversationDto>(HttpMethod.Get, $"api/conversations/{id}", null, auth: true, ct);

    public Task<ConversationDto> CreateConversationAsync(ConversationCreateRequest request, CancellationToken ct = default) =>
        SendAsync<ConversationDto>(HttpMethod.Post, "api/conversations", request, auth: true, ct);

    public Task AddParticipantAsync(int conversationId, AddParticipantRequest request, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Post, $"api/conversations/{conversationId}/participants", request, ct);

    public Task RemoveParticipantAsync(int conversationId, int userId, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"api/conversations/{conversationId}/participants/{userId}", null, ct);

    // -------------------------------------------------------------- Messages

    public Task<PagedMessageResponse> GetMessagesAsync(int conversationId, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        return SendAsync<PagedMessageResponse>(
            HttpMethod.Get,
            $"api/conversations/{conversationId}/messages?page={page}&pageSize={pageSize}",
            null,
            auth: true,
            ct);
    }

    public Task<MessageDto> SendMessageAsync(int conversationId, string content, CancellationToken ct = default) =>
        SendAsync<MessageDto>(
            HttpMethod.Post,
            $"api/conversations/{conversationId}/messages",
            new MessageCreateRequest { Content = content },
            auth: true,
            ct);

    // ------------------------------------------------------------- Internals

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, bool auth, CancellationToken ct)
    {
        using var response = await SendRawAsync(method, path, body, auth, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (string.IsNullOrWhiteSpace(payload))
            throw new ApiException("Le serveur a renvoyé une réponse vide.", (int)response.StatusCode);

        try
        {
            var result = JsonSerializer.Deserialize<T>(payload, JsonOptions);
            if (result is null)
                throw new ApiException("Réponse du serveur illisible.", (int)response.StatusCode);
            return result;
        }
        catch (JsonException ex)
        {
            throw new ApiException("Réponse du serveur illisible.", (int)response.StatusCode, ex);
        }
    }

    private async Task SendNoContentAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var _ = await SendRawAsync(method, path, body, auth: true, ct);
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string path, object? body, bool auth, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, BuildUri(path));

        if (auth && !string.IsNullOrWhiteSpace(_token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            request.Dispose();
            throw new ApiException("Le serveur met trop de temps à répondre. Vérifiez son adresse.", 0, ex);
        }
        catch (HttpRequestException ex)
        {
            request.Dispose();
            throw new ApiException("Serveur OrgChat injoignable. Vérifiez l'adresse et le réseau.", 0, ex);
        }

        request.Dispose();

        if (response.IsSuccessStatusCode)
            return response;

        var raw = await response.Content.ReadAsStringAsync(ct);
        var status = (int)response.StatusCode;
        response.Dispose();

        throw new ApiException(ExtractError(raw, status), status);
    }

    private Uri BuildUri(string path) =>
        new($"{_baseUrl}/{path.TrimStart('/')}", UriKind.Absolute);

    private static string ExtractError(string raw, int status)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var document = JsonDocument.Parse(raw);
                var root = document.RootElement;

                if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                    return error.GetString() ?? DefaultError(status);

                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    var messages = new List<string>();
                    foreach (var field in errors.EnumerateObject())
                    {
                        if (field.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in field.Value.EnumerateArray())
                            {
                                var text = item.GetString();
                                if (!string.IsNullOrWhiteSpace(text))
                                    messages.Add(text!);
                            }
                        }
                    }

                    if (messages.Count > 0)
                        return string.Join(" ", messages.Distinct());
                }

                if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                    return title.GetString() ?? DefaultError(status);
            }
            catch (JsonException)
            {
                var trimmed = raw.Trim();
                if (trimmed.Length <= 220 && !trimmed.StartsWith('<'))
                    return trimmed;
            }
        }

        return DefaultError(status);
    }

    private static string DefaultError(int status) => status switch
    {
        (int)HttpStatusCode.Unauthorized => "Identifiants invalides ou session expirée.",
        (int)HttpStatusCode.Forbidden => "Vous n'avez pas les droits nécessaires.",
        (int)HttpStatusCode.NotFound => "Élément introuvable.",
        (int)HttpStatusCode.BadRequest => "La requête est invalide.",
        _ => $"Erreur serveur ({status})."
    };

    private static string Normalize(string url)
    {
        url = (url ?? string.Empty).Trim();
        if (url.Length == 0)
            return "http://localhost:5000";

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "http://" + url;
        }

        return url.TrimEnd('/');
    }

    public void Dispose() => _http.Dispose();
}
