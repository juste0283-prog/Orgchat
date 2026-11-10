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

    /// <summary>Activation première connexion / réinitialisation via jeton à usage unique.</summary>
    public Task<AuthResponse> ActivateAsync(ActivateAccountRequest request, CancellationToken ct = default) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/activate", request, auth: false, ct);

    // ----------------------------------------------------------------- Users

    public Task<UserDto> GetMeAsync(CancellationToken ct = default) =>
        SendAsync<UserDto>(HttpMethod.Get, "api/users/me", null, auth: true, ct);

    public Task<List<UserDto>> GetUsersAsync(CancellationToken ct = default) =>
        SendAsync<List<UserDto>>(HttpMethod.Get, "api/users", null, auth: true, ct);

    public Task<UserDto> GetUserAsync(int id, CancellationToken ct = default) =>
        SendAsync<UserDto>(HttpMethod.Get, $"api/users/{id}", null, auth: true, ct);

    public Task<UserDto> UpdateUserAsync(int id, UpdateUserRequest request, CancellationToken ct = default) =>
        SendAsync<UserDto>(HttpMethod.Put, $"api/users/{id}", request, auth: true, ct);

    public Task<CreatedUserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default) =>
        SendAsync<CreatedUserDto>(HttpMethod.Post, "api/users", request, auth: true, ct);

    /// <summary>Émet un nouveau lien d'activation pour un compte en attente.</summary>
    public Task<CreatedUserDto> ResendActivationAsync(int id, CancellationToken ct = default) =>
        SendAsync<CreatedUserDto>(HttpMethod.Post, $"api/users/{id}/resend-activation", null, auth: true, ct);

    /// <summary>
    /// Transforme le lien relatif renvoyé par l'API en lien absolu utilisable
    /// par l'administrateur, avec repli sur le jeton si nécessaire.
    /// </summary>
    public string ResolveActivationLink(CreatedUserDto user)
    {
        if (!string.IsNullOrWhiteSpace(user.ActivationLink))
        {
            if (Uri.TryCreate(user.ActivationLink, UriKind.Absolute, out var absolute))
                return absolute.ToString();

            return new Uri(
                new Uri(BaseUrl.TrimEnd('/') + "/"),
                user.ActivationLink.TrimStart('/')).ToString();
        }

        if (!string.IsNullOrWhiteSpace(user.ActivationToken))
        {
            return new Uri(
                new Uri(BaseUrl.TrimEnd('/') + "/"),
                $"api/auth/activate?token={Uri.EscapeDataString(user.ActivationToken)}").ToString();
        }

        return string.Empty;
    }

    public Task<UserDto> UpdateUserRoleAsync(int id, UpdateUserRoleRequest request, CancellationToken ct = default) =>
        SendAsync<UserDto>(HttpMethod.Put, $"api/users/{id}/role", request, auth: true, ct);

    public Task ChangeMyPasswordAsync(ChangePasswordRequest request, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Put, "api/users/me/password", request, ct);

    public Task ResetUserPasswordAsync(int id, ResetPasswordRequest request, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Put, $"api/users/{id}/password", request, ct);

    public Task DeleteUserAsync(int id, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"api/users/{id}", null, ct);

    public Task DeactivateUserAsync(int id, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Put, $"api/users/{id}/deactivate", null, ct);

    public Task ReactivateUserAsync(int id, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Put, $"api/users/{id}/reactivate", null, ct);

    // --------------------------------------------------------- Conversations

    public Task<List<ConversationDto>> GetConversationsAsync(CancellationToken ct = default) =>
        SendAsync<List<ConversationDto>>(HttpMethod.Get, "api/conversations", null, auth: true, ct);

    public Task<ConversationDto> GetConversationAsync(int id, CancellationToken ct = default) =>
        SendAsync<ConversationDto>(HttpMethod.Get, $"api/conversations/{id}", null, auth: true, ct);

    public Task<ReadReceiptDto> MarkConversationReadAsync(int id, CancellationToken ct = default) =>
        SendAsync<ReadReceiptDto>(HttpMethod.Post, $"api/conversations/{id}/read", null, auth: true, ct);

    public Task<ConversationDto> CreateConversationAsync(ConversationCreateRequest request, CancellationToken ct = default) =>
        SendAsync<ConversationDto>(HttpMethod.Post, "api/conversations", request, auth: true, ct);

    public Task AddParticipantAsync(int conversationId, AddParticipantRequest request, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Post, $"api/conversations/{conversationId}/participants", request, ct);

    public Task RemoveParticipantAsync(int conversationId, int userId, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"api/conversations/{conversationId}/participants/{userId}", null, ct);

    public Task LeaveConversationAsync(int conversationId, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Post, $"api/conversations/{conversationId}/leave", null, ct);

    public Task DeleteConversationAsync(int conversationId, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"api/conversations/{conversationId}", null, ct);

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

    public Task<MessageDto> UpdateMessageAsync(int conversationId, int messageId, string content, CancellationToken ct = default) =>
        SendAsync<MessageDto>(
            HttpMethod.Put,
            $"api/conversations/{conversationId}/messages/{messageId}",
            new MessageUpdateRequest { Content = content },
            auth: true,
            ct);

    public Task<MessageDeletedDto> DeleteMessageAsync(int conversationId, int messageId, CancellationToken ct = default) =>
        SendAsync<MessageDeletedDto>(
            HttpMethod.Delete,
            $"api/conversations/{conversationId}/messages/{messageId}",
            null,
            auth: true,
            ct);

    public Task<List<MessageSearchResultDto>> SearchMessagesAsync(string query, int take = 50, CancellationToken ct = default) =>
        SendAsync<List<MessageSearchResultDto>>(
            HttpMethod.Get,
            $"api/search/messages?q={Uri.EscapeDataString(query)}&take={Math.Clamp(take, 1, 100)}",
            null,
            auth: true,
            ct);

    public async Task<MessageDto> UploadAttachmentAsync(
        int conversationId,
        string filePath,
        string? content,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new ApiException("Le fichier sélectionné est introuvable.", 0);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUri($"api/conversations/{conversationId}/messages/attachments"));

        if (!string.IsNullOrWhiteSpace(_token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        await using var stream = File.OpenRead(filePath);
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(ContentTypeFor(filePath));
        form.Add(fileContent, "file", Path.GetFileName(filePath));

        if (!string.IsNullOrWhiteSpace(content))
            form.Add(new StringContent(content, Encoding.UTF8), "content");

        request.Content = form;

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ApiException("Le serveur met trop de temps à répondre. Vérifiez son adresse.", 0, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException("Serveur OrgChat injoignable. Vérifiez l'adresse et le réseau.", 0, ex);
        }

        using (response)
        {
            var payload = await response.Content.ReadAsStringAsync(ct);
            var status = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
                throw new ApiException(ExtractError(payload, status), status);

            try
            {
                return JsonSerializer.Deserialize<MessageDto>(payload, JsonOptions)
                    ?? throw new ApiException("Réponse du serveur illisible.", status);
            }
            catch (JsonException ex)
            {
                throw new ApiException("Réponse du serveur illisible.", status, ex);
            }
        }
    }

    public async Task<byte[]> DownloadAttachmentAsync(int attachmentId, CancellationToken ct = default)
    {
        using var response = await SendRawAsync(
            HttpMethod.Get, $"api/attachments/{attachmentId}", null, auth: true, ct);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    // -------------------------------------------------------- Notifications

    public Task<List<NotificationDto>> GetNotificationsAsync(int take = 100, int skip = 0, CancellationToken ct = default) =>
        SendAsync<List<NotificationDto>>(
            HttpMethod.Get,
            $"api/notifications?take={Math.Clamp(take, 1, 200)}&skip={Math.Max(0, skip)}",
            null,
            auth: true,
            ct);

    public Task DeleteNotificationAsync(int id, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"api/notifications/{id}", null, ct);

    public Task ClearNotificationsAsync(CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, "api/notifications", null, ct);

    public Task<NotificationCountDto> GetNotificationUnreadCountAsync(CancellationToken ct = default) =>
        SendAsync<NotificationCountDto>(
            HttpMethod.Get, "api/notifications/unread-count", null, auth: true, ct);

    public Task<NotificationDto> MarkNotificationReadAsync(int id, CancellationToken ct = default) =>
        SendAsync<NotificationDto>(
            HttpMethod.Post, $"api/notifications/{id}/read", null, auth: true, ct);

    public Task MarkAllNotificationsReadAsync(CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Post, "api/notifications/read-all", null, ct);

    // ------------------------------------------------------- Organisations

    public Task<OrganizationDto> GetCurrentOrganizationAsync(CancellationToken ct = default) =>
        SendAsync<OrganizationDto>(HttpMethod.Get, "api/organizations/current", null, auth: true, ct);

    public Task<List<OrganizationDto>> GetOrganizationsAsync(CancellationToken ct = default) =>
        SendAsync<List<OrganizationDto>>(HttpMethod.Get, "api/organizations", null, auth: true, ct);

    // ------------------------------------------------------------ Départements

    public Task<List<DepartmentDto>> GetDepartmentsAsync(CancellationToken ct = default) =>
        SendAsync<List<DepartmentDto>>(HttpMethod.Get, "api/departments", null, auth: true, ct);

    public Task<DepartmentDetailDto> GetDepartmentAsync(int id, CancellationToken ct = default) =>
        SendAsync<DepartmentDetailDto>(HttpMethod.Get, $"api/departments/{id}", null, auth: true, ct);

    public Task<DepartmentDto> CreateDepartmentAsync(DepartmentCreateRequest request, CancellationToken ct = default) =>
        SendAsync<DepartmentDto>(HttpMethod.Post, "api/departments", request, auth: true, ct);

    public Task<DepartmentDto> UpdateDepartmentAsync(int id, DepartmentUpdateRequest request, CancellationToken ct = default) =>
        SendAsync<DepartmentDto>(HttpMethod.Put, $"api/departments/{id}", request, auth: true, ct);

    public Task DeleteDepartmentAsync(int id, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"api/departments/{id}", null, ct);

    public Task AddDepartmentMemberAsync(int id, int userId, bool isPrimary = false, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Post, $"api/departments/{id}/members",
            new AddDepartmentMemberRequest { UserId = userId, IsPrimary = isPrimary }, ct);

    public Task RemoveDepartmentMemberAsync(int id, int userId, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"api/departments/{id}/members/{userId}", null, ct);

    public Task<DepartmentStatsDto> GetDepartmentStatsAsync(int id, CancellationToken ct = default) =>
        SendAsync<DepartmentStatsDto>(HttpMethod.Get, $"api/departments/{id}/stats", null, auth: true, ct);

    // ------------------------------------------------------------- Supervision

    public Task<SupervisionSummaryDto> GetSupervisionSummaryAsync(CancellationToken ct = default) =>
        SendAsync<SupervisionSummaryDto>(HttpMethod.Get, "api/supervision/summary", null, auth: true, ct);

    public Task<PagedResultDto<SupervisionUserDto>> GetSupervisionUsersAsync(
        string? search = null,
        string? status = null,
        string? role = null,
        int? departmentId = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        var query = BuildQuery(
            ("search", search),
            ("status", status),
            ("role", role),
            ("departmentId", departmentId?.ToString()),
            ("page", Math.Max(1, page).ToString()),
            ("pageSize", Math.Clamp(pageSize, 1, 100).ToString()));
        return SendAsync<PagedResultDto<SupervisionUserDto>>(
            HttpMethod.Get, $"api/supervision/users{query}", null, auth: true, ct);
    }

    public Task<List<DepartmentSupervisionDto>> GetSupervisionDepartmentsAsync(CancellationToken ct = default) =>
        SendAsync<List<DepartmentSupervisionDto>>(HttpMethod.Get, "api/supervision/departments", null, auth: true, ct);

    public Task<List<ActivityPointDto>> GetSupervisionActivityAsync(int days = 30, CancellationToken ct = default) =>
        SendAsync<List<ActivityPointDto>>(
            HttpMethod.Get,
            $"api/supervision/activity?days={Math.Clamp(days, 1, 90)}",
            null,
            auth: true,
            ct);

    public Task<SupervisionConnectionsDto> GetSupervisionConnectionsAsync(int hours = 24, CancellationToken ct = default) =>
        SendAsync<SupervisionConnectionsDto>(
            HttpMethod.Get,
            $"api/supervision/connections?hours={Math.Clamp(hours, 1, 168)}",
            null,
            auth: true,
            ct);

    public Task<SupervisionHealthDto> GetSupervisionHealthAsync(CancellationToken ct = default) =>
        SendAsync<SupervisionHealthDto>(HttpMethod.Get, "api/supervision/health", null, auth: true, ct);

    public Task<PagedResultDto<IncidentDto>> GetSupervisionIncidentsAsync(
        int days = 7,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default) =>
        SendAsync<PagedResultDto<IncidentDto>>(
            HttpMethod.Get,
            $"api/supervision/incidents?days={Math.Clamp(days, 1, 90)}&page={Math.Max(1, page)}&pageSize={Math.Clamp(pageSize, 1, 100)}",
            null,
            auth: true,
            ct);

    public Task<SupervisionReportDto> GetSupervisionReportAsync(string period = "daily", CancellationToken ct = default) =>
        SendAsync<SupervisionReportDto>(
            HttpMethod.Get,
            $"api/supervision/reports?period={Uri.EscapeDataString(period)}",
            null,
            auth: true,
            ct);

    public Task<PagedResultDto<AuditLogDto>> GetAuditLogsAsync(
        string? search = null,
        string? action = null,
        string? entityType = null,
        string? result = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        var query = BuildQuery(
            ("search", search),
            ("action", action),
            ("entityType", entityType),
            ("result", result),
            ("page", Math.Max(1, page).ToString()),
            ("pageSize", Math.Clamp(pageSize, 1, 100).ToString()));
        return SendAsync<PagedResultDto<AuditLogDto>>(
            HttpMethod.Get, $"api/audit{query}", null, auth: true, ct);
    }

    public Task<AuditFacetsDto> GetAuditFacetsAsync(CancellationToken ct = default) =>
        SendAsync<AuditFacetsDto>(HttpMethod.Get, "api/audit/facets", null, auth: true, ct);

    private static string BuildQuery(params (string Key, string? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}")
            .ToList();
        return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
    }

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

    private static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        ".txt" or ".log" or ".csv" => "text/plain",
        ".json" => "application/json",
        ".zip" => "application/zip",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".ppt" => "application/vnd.ms-powerpoint",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        _ => "application/octet-stream"
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
