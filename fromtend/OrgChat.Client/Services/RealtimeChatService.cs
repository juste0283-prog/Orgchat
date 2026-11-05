using Microsoft.AspNetCore.SignalR.Client;
using OrgChat.Client.Models;

namespace OrgChat.Client.Services;

public enum RealtimeState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting
}

/// <summary>
/// Encapsule la connexion SignalR au hub /hubs/chat.
/// Le jeton JWT est fourni au hub via AccessTokenProvider.
/// </summary>
public sealed class RealtimeChatService : IAsyncDisposable
{
    private readonly ApiClient _api;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HubConnection? _connection;
    private bool _disposed;

    public RealtimeChatService(ApiClient api)
    {
        _api = api;
    }

    public RealtimeState State { get; private set; } = RealtimeState.Disconnected;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event Action<MessageDto>? MessageReceived;

    public event Action<RealtimeState, string?>? StateChanged;

    public event Func<Task>? Reconnected;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(ct);
        try
        {
            if (_connection is { State: HubConnectionState.Connected })
                return;

            if (_connection is null)
                BuildConnection();

            SetState(RealtimeState.Connecting, null);

            try
            {
                await _connection!.StartAsync(ct);
                SetState(RealtimeState.Connected, null);
            }
            catch (Exception ex)
            {
                SetState(RealtimeState.Disconnected, ex.Message);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connection is null)
            return;

        try
        {
            await _connection.StopAsync();
        }
        catch
        {
            // Déconnexion best-effort.
        }
        finally
        {
            SetState(RealtimeState.Disconnected, null);
        }
    }

    public async Task<bool> JoinConversationAsync(int conversationId, CancellationToken ct = default)
    {
        if (_connection is not { State: HubConnectionState.Connected })
            return false;

        try
        {
            await _connection.InvokeAsync("JoinConversation", conversationId, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task LeaveConversationAsync(int conversationId, CancellationToken ct = default)
    {
        if (_connection is not { State: HubConnectionState.Connected })
            return;

        try
        {
            await _connection.InvokeAsync("LeaveConversation", conversationId, ct);
        }
        catch
        {
            // Sans effet si le groupe a déjà été quitté.
        }
    }

    public async Task SendMessageAsync(int conversationId, string content, CancellationToken ct = default)
    {
        if (_connection is not { State: HubConnectionState.Connected })
            throw new InvalidOperationException("La connexion temps réel n'est pas active.");

        await _connection.InvokeAsync("SendMessage", conversationId, content, ct);
    }

    public async Task<PagedMessageResponse?> GetHistoryAsync(int conversationId, int page, int pageSize, CancellationToken ct = default)
    {
        if (_connection is not { State: HubConnectionState.Connected })
            return null;

        return await _connection.InvokeAsync<PagedMessageResponse>(
            "GetHistory", conversationId, page, pageSize, ct);
    }

    private void BuildConnection()
    {
        _connection = new HubConnectionBuilder()
            .WithUrl($"{_api.BaseUrl}/hubs/chat", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_api.Token);
            })
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(20)
            })
            .Build();

        _connection.On<MessageDto>("ReceiveMessage", message => MessageReceived?.Invoke(message));

        _connection.Reconnecting += error =>
        {
            SetState(RealtimeState.Reconnecting, error?.Message);
            return Task.CompletedTask;
        };

        _connection.Reconnected += _ =>
        {
            SetState(RealtimeState.Connected, null);
            Reconnected?.Invoke();
            return Task.CompletedTask;
        };

        _connection.Closed += error =>
        {
            SetState(RealtimeState.Disconnected, error?.Message);
            return Task.CompletedTask;
        };
    }

    private void SetState(RealtimeState state, string? detail)
    {
        State = state;
        StateChanged?.Invoke(state, detail);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_connection is not null)
        {
            try
            {
                await _connection.DisposeAsync();
            }
            catch
            {
                // Rien à faire.
            }
        }

        _gate.Dispose();
    }
}
