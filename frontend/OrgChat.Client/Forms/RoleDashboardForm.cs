using OrgChat.Client.Configuration;
using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>
/// Tableau de bord par rôle (SuperAdmin, OrgAdmin, Superviseur,
/// Responsable de département). Les utilisateurs simples ouvrent
/// directement la messagerie (MainForm).
/// Les statistiques proviennent des endpoints réels (users, départements,
/// conversations) — aucun chiffre fictif.
/// </summary>
public sealed class RoleDashboardForm : Form
{
    private readonly ApiClient _api;
    private readonly AuthResponse _auth;
    private readonly Label _titleLabel = null!;
    private readonly Label _subtitleLabel = null!;
    private Label? _statsLabel;
    private Label? _errorLabel;
    private RoundedButton? _usersButton;
    private RoundedButton? _departmentsButton;
    private RoundedButton? _supervisionButton;

    public event EventHandler? OpenMessagingRequested;
    public event EventHandler? LogoutRequested;
    public event EventHandler? SettingsChanged;

    public RoleDashboardForm(ApiClient api, ClientOptions options, AuthResponse auth)
    {
        _api = api;
        _auth = auth;

        Text = "OrgChat · Tableau de bord";
        ClientSize = new Size(1020, 700);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Palette.Background;
        ForeColor = Palette.Text;
        Font = Palette.Body;
        DoubleBuffered = true;

        var role = auth.Role;
        var title = role switch
        {
            "SuperAdmin" => "Dashboard Super administrateur",
            "OrgAdmin" => "Dashboard administrateur",
            "Supervisor" => "Tableau de bord superviseur",
            "DepartmentManager" => "Dashboard responsable de département",
            _ => "Dashboard",
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(24),
            BackColor = Palette.Background,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _titleLabel = Ui.Label(title, new Font("Segoe UI", 20f, FontStyle.Bold), Palette.Text);
        root.Controls.Add(_titleLabel, 0, 0);

        _subtitleLabel = Ui.Label(
            $"{auth.DisplayName} (@{auth.Username}) · Rôle : {RoleLabel(role)}",
            Palette.Body,
            Palette.Muted);
        root.Controls.Add(_subtitleLabel, 0, 1);

        _statsLabel = Ui.Label("Chargement des statistiques…", Palette.Body, Palette.Muted);
        root.Controls.Add(_statsLabel, 0, 2);

        _errorLabel = Ui.Label(string.Empty, Palette.Small, Palette.Danger);
        root.Controls.Add(_errorLabel, 0, 3);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Palette.Background,
        };

        var openMessaging = Ui.Primary("Ouvrir la messagerie", 220, 44);
        openMessaging.Click += (_, _) => OpenMessagingRequested?.Invoke(this, EventArgs.Empty);
        actions.Controls.Add(openMessaging);

        var profileButton = Ui.Outline("Mon profil", 220, 44);
        profileButton.Click += OnProfileClicked;
        actions.Controls.Add(profileButton);

        _usersButton = Ui.Outline("Gérer les utilisateurs", 220, 44);
        _usersButton.Click += OnManageUsersClicked;
        actions.Controls.Add(_usersButton);

        _departmentsButton = Ui.Outline("Voir les départements", 220, 44);
        _departmentsButton.Click += OnManageDepartmentsClicked;
        actions.Controls.Add(_departmentsButton);

        _supervisionButton = Ui.Outline("Supervision (métadonnées)", 220, 44);
        _supervisionButton.Click += OnSupervisionClicked;
        actions.Controls.Add(_supervisionButton);

        var settings = Ui.Outline("⚙ Paramètres", 180, 44);
        settings.Click += (_, _) => SettingsChanged?.Invoke(this, EventArgs.Empty);
        actions.Controls.Add(settings);

        var logout = Ui.Outline("Se déconnecter", 180, 44);
        logout.Click += (_, _) => LogoutRequested?.Invoke(this, EventArgs.Empty);
        actions.Controls.Add(logout);

        root.Controls.Add(actions, 0, 4);
        Controls.Add(root);

        ApplyPermissions();
        Localization.Apply(this);
        _ = LoadStatsAsync();
    }

    private void ApplyPermissions()
    {
        // Vérifications UI uniquement : l'autorisation réelle reste
        // vérifiée côté backend (403 si refusé).
        var isAdmin = _auth.Role is "SuperAdmin" or "OrgAdmin";
        if (_usersButton != null)
            _usersButton.Enabled = isAdmin;

        if (_departmentsButton != null)
            _departmentsButton.Enabled = isAdmin || _auth.Role == "DepartmentManager";

        if (_supervisionButton != null)
            _supervisionButton.Enabled = _auth.Role is "SuperAdmin" or "OrgAdmin" or "Supervisor" or "DepartmentManager";
    }

    private async Task LoadStatsAsync()
    {
        try
        {
            var usersTask = SafeAsync(() => _api.GetUsersAsync());
            var conversationsTask = SafeAsync(() => _api.GetConversationsAsync());
            var departmentsTask = SafeAsync(() => _api.GetDepartmentsAsync());

            var users = await usersTask;
            var conversations = await conversationsTask;
            var departments = await departmentsTask;

            var active = users?.Count(u => u.IsActive) ?? 0;
            _statsLabel!.Text =
                $"Utilisateurs : {users?.Count ?? 0} ({active} actifs) · " +
                $"Conversations : {conversations?.Count ?? 0} · " +
                $"Départements : {departments?.Count ?? 0}";
        }
        catch (Exception ex)
        {
            _statsLabel!.Text = "Statistiques indisponibles.";
            _errorLabel!.Text = Ui.FriendlyError(ex);
        }
    }

    private async Task<T?> SafeAsync<T>(Func<Task<T>> factory)
    {
        try
        {
            return await factory();
        }
        catch
        {
            return default;
        }
    }

    private async void OnProfileClicked(object? sender, EventArgs e)
    {
        try
        {
            var me = await _api.GetMeAsync();
            using var form = new ProfileForm(_api, me);
            // Nom affiché + e-mail + bouton « Changer mon mot de passe »
            // (PUT /api/users/me et PUT /api/users/me/password).
            if (form.ShowDialog(this) == DialogResult.OK && form.UpdatedUser != null)
            {
                _auth.DisplayName = form.UpdatedUser.DisplayName;
                _auth.Email = form.UpdatedUser.Email;
                _subtitleLabel.Text =
                    $"{_auth.DisplayName} (@{_auth.Username}) · Rôle : {RoleLabel(_auth.Role)}";
            }
        }
        catch (Exception ex)
        {
            ShowError(Ui.FriendlyError(ex));
        }
    }

    private async void OnManageUsersClicked(object? sender, EventArgs e)
    {
        try
        {
            var users = await _api.GetUsersAsync();
            using var form = new UsersForm(_api, users, _auth.UserId, _auth.IsAdmin);
            form.ShowDialog(this);
            _ = LoadStatsAsync();
        }
        catch (Exception ex)
        {
            ShowError(Ui.FriendlyError(ex));
        }
    }

    private async void OnManageDepartmentsClicked(object? sender, EventArgs e)
    {
        try
        {
            var departments = await _api.GetDepartmentsAsync();
            var list = departments.Count == 0
                ? "Aucun département."
                : string.Join(Environment.NewLine, departments.Select(d => $"• {d.Name} — {d.MemberCount} membre(s)"));
            MessageBox.Show(this, list, "Départements de votre organisation", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _ = LoadStatsAsync();
        }
        catch (Exception ex)
        {
            ShowError(Ui.FriendlyError(ex));
        }
    }

    private async void OnSupervisionClicked(object? sender, EventArgs e)
    {
        try
        {
            var users = await _api.GetUsersAsync();
            var online = users.Count(u => u.IsOnline);
            var active = users.Count(u => u.IsActive);
            MessageBox.Show(this,
                $"Supervision (métadonnées uniquement, contenu des conversations non accessible) :\n\n" +
                $"Utilisateurs inscrits : {users.Count}\n" +
                $"Comptes actifs : {active}\n" +
                $"Utilisateurs en ligne : {online}\n\n" +
                "Le détail par département et les rapports seront détaillés dans le module de supervision.",
                "Supervision",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowError(Ui.FriendlyError(ex));
        }
    }

    private void ShowError(string message)
    {
        if (_errorLabel is null)
            return;
        _errorLabel.Text = message;
        _errorLabel.ForeColor = Palette.Danger;
        _errorLabel.Visible = !string.IsNullOrWhiteSpace(message);
    }

    private static string RoleLabel(string roleKey) => roleKey switch
    {
        "SuperAdmin" => "Super administrateur",
        "OrgAdmin" => "Administrateur d'organisation",
        "Supervisor" => "Superviseur",
        "DepartmentManager" => "Responsable de département",
        _ => "Utilisateur",
    };
}
