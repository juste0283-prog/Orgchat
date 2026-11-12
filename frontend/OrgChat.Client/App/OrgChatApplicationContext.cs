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
    private RoleDashboardForm? _dashboard;
    private AuthResponse? _auth;
    private int? _pendingConversationId;

    public OrgChatApplicationContext(ApiClient api, ClientOptions options)
    {
        _api = api;
        _options = options;
        // Exige une authentification explicite à chaque lancement : aucune session
        // persistée n'est restaurée automatiquement.
        _api.SetToken(null);
        SessionStore.Clear();
        ShowLogin();
    }

    private static bool NeedsDashboard(AuthResponse auth) =>
        auth.Role is "SuperAdmin" or "OrgAdmin" or "Supervisor" or "DepartmentManager";

    private async Task TryRestoreSessionAsync()
    {
        var saved = await SessionStore.LoadAsync();
        if (saved is null || (_main is not null || _dashboard is not null))
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

            if (_main is null && _dashboard is null)
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
            if (ReferenceEquals(_login, login) && _main is null && _dashboard is null)
                ExitThread();
        };
        login.Show();
    }

    private void OnAuthenticated(AuthResponse auth)
    {
        _auth = auth;
        _login?.Hide();

        // Seuls les utilisateurs simples ouvrent directement la messagerie.
        // Toute autre rôle (SuperAdmin, OrgAdmin, Superviseur, Responsable
        // de département) atterrit d'abord sur son tableau de bord.
        if (NeedsDashboard(auth))
            CreateDashboard(auth);
        else
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
            Logout();
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

    private void CreateDashboard(AuthResponse auth)
    {
        var dashboard = new RoleDashboardForm(_api, _options, auth);
        _dashboard = dashboard;

        dashboard.SettingsChanged += (_, _) => RebuildUi();

        dashboard.LogoutRequested += (_, _) =>
        {
            var old = _dashboard;
            _dashboard = null;
            old?.Close();
            RefreshAfterLogout();
        };

        dashboard.OpenMessagingRequested += (_, _) =>
        {
            // Garde le dashboard ouvert (masqué) : retour possible via la messagerie.
            dashboard.Hide();
            CreateMain(auth);
            dashboard.Show();
        };

        dashboard.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_dashboard, dashboard) && _main is null)
            {
                _dashboard = null;
                ExitThread();
            }
        };

        dashboard.Show();
        dashboard.Activate();
    }

    private void RefreshAfterLogout()
    {
        _auth = null;
        _api.SetToken(null);
        SessionStore.Clear();

        var oldLogin = _login;
        _login = null;
        oldLogin?.Dispose();
        ShowLogin();
    }

    private void Logout()
    {
        _auth = null;
        _api.SetToken(null);
        SessionStore.Clear();

        var oldLogin = _login;
        _login = null;
        oldLogin?.Dispose();
        ShowLogin();
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

        if (_dashboard is not null)
        {
            var dashboard = _dashboard;
            _dashboard = null;
            dashboard.Close();
            if (_auth is not null)
                CreateDashboard(_auth);
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
