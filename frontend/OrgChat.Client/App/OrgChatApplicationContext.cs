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
        var login = new LoginForm(_api, _options, OnAuthenticated);
        _login = login;
        login.FormClosed += (_, _) =>
        {
            if (_main is null)
                ExitThread();
        };
        login.Show();
    }

    private void OnAuthenticated(AuthResponse auth)
    {
        _login?.Hide();

        var main = new MainForm(_api, _options, auth);
        _main = main;

        main.LogoutRequested += (_, _) =>
        {
            _main = null;
            main.CloseForLogout();
            _api.SetToken(null);
            SessionStore.Clear();
            _login?.ResetForLogout();
            _login?.Show();
            _login?.Activate();
        };

        main.FormClosed += (_, _) =>
        {
            if (_main == main)
            {
                _main = null;
                ExitThread();
            }
        };

        main.Show();
        main.Activate();
    }
}
