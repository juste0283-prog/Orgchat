using OrgChat.Client.Configuration;
using OrgChat.Client.Forms;
using OrgChat.Client.Models;
using OrgChat.Client.Services;

namespace OrgChat.Client.App;

/// <summary>Cycle de vie de l'application : connexion puis fenêtre principale.</summary>
public sealed class OrgChatApplicationContext : ApplicationContext
{
    private readonly ApiClient _api;
    private readonly ClientOptions _options;
    private LoginForm? _login;
    private MainForm? _main;
    private AuthResponse? _auth;
    private int? _pendingConversationId;

    public OrgChatApplicationContext(ApiClient api, ClientOptions options)
    {
        _api = api;
        _options = options;
        ShowLogin();
        _ = TryRestoreSessionAsync();
    }

    private async Task TryRestoreSessionAsync()
    {
        var saved = await SessionStore.LoadAsync();
        if (saved is null || _main is not null)
            return;

        var previousBaseUrl = _api.BaseUrl;
        _api.SetBaseUrl(saved.BaseUrl);
        _api.SetToken(saved.Auth.Token);

        try
        {
            var me = await _api.GetMeAsync();
            if (!me.IsActive)
                throw new InvalidOperationException("Compte désactivé.");

            saved.Auth.UserId = me.Id;
            saved.Auth.Username = me.Username;
            saved.Auth.Email = me.Email;
            saved.Auth.DisplayName = me.DisplayName;
            saved.Auth.Role = me.Role;

            if (_main is null)
            {
                _login?.Hide();
                OnAuthenticated(saved.Auth);
            }
        }
        catch
        {
            _api.SetToken(null);
            _api.SetBaseUrl(previousBaseUrl);
            SessionStore.Clear();
        }
    }

    private void ShowLogin()
    {
        var login = new LoginForm(_api, _options, OnAuthenticated, RebuildUi);
        _login = login;
        login.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_login, login) && _main is null)
                ExitThread();
        };
        login.Show();
    }

    private void OnAuthenticated(AuthResponse auth)
    {
        _auth = auth;
        _login?.Hide();
        CreateMain(auth);
    }

    private void CreateMain(AuthResponse auth)
    {
        var main = new MainForm(_api, _options, auth, _pendingConversationId);
        _pendingConversationId = null;
        _main = main;

        main.SettingsChanged += (_, _) => RebuildUi();

        main.LogoutRequested += (_, _) =>
        {
            var oldMain = _main;
            _main = null;
            oldMain?.CloseForLogout();
            _auth = null;
            _api.SetToken(null);
            SessionStore.Clear();

            var oldLogin = _login;
            _login = null;
            oldLogin?.Dispose();
            ShowLogin();
        };

        main.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_main, main))
            {
                _main = null;
                ExitThread();
            }
        };

        main.Show();
        main.Activate();
    }

    /// <summary>Reconstruit l'interface après un changement de langue ou de thème.</summary>
    private void RebuildUi()
    {
        if (_main is not null)
        {
            var main = _main;
            _pendingConversationId = main.CurrentConversationId;
            _main = null;
            main.CloseForRebuild();

            if (_auth is not null)
                CreateMain(_auth);
            return;
        }

        if (_login is null)
            return;

        var oldLogin = _login;
        _login = null;
        oldLogin.Hide();
        oldLogin.Dispose();
        ShowLogin();
    }
}
