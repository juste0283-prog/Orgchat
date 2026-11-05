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
            main.Close();
            _api.SetToken(null);
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
