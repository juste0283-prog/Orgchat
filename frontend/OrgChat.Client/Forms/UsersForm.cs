using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>Annuaire des comptes, avec actions d'administration.</summary>
public sealed class UsersForm : Form
{
    private readonly ApiClient _api;
    private readonly int _currentUserId;
    private readonly bool _isAdmin;
    private readonly List<UserDto> _users;
    private readonly SmoothFlowPanel _listPanel;
    private readonly TextBox _searchBox;
    private readonly Label _subtitle;
    private readonly Label _errorLabel;
    private bool _busy;

    public UsersForm(ApiClient api, IEnumerable<UserDto> users, int currentUserId, bool isAdmin)
    {
        _api = api;
        _currentUserId = currentUserId;
        _isAdmin = isAdmin;
        _users = users.ToList();

        Ui.StyleDialog(this);
        Text = "Annuaire";
        ClientSize = new Size(780, 700);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(26),
            BackColor = Palette.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var title = Ui.Label("Annuaire de l'organisation", new Font("Segoe UI", 16f, FontStyle.Bold));
        title.Location = new Point(0, 0);
        _subtitle = Ui.Label(string.Empty, Palette.Small, Palette.Muted);
        _subtitle.Location = new Point(0, 30);
        header.Controls.Add(title);
        header.Controls.Add(_subtitle);
        root.Controls.Add(header, 0, 0);

        var searchField = Ui.SearchField("Rechercher un compte…", out _searchBox, height: 40);
        searchField.Dock = DockStyle.Top;
        _searchBox.TextChanged += (_, _) => RebuildRows();
        root.Controls.Add(searchField, 0, 1);

        _listPanel = new SmoothFlowPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Palette.Background,
            Padding = new Padding(0, 4, 0, 4)
        };
        _listPanel.Resize += (_, _) => ResizeRows();
        root.Controls.Add(_listPanel, 0, 2);

        _errorLabel = Ui.Label(string.Empty, Palette.Small, Palette.Danger, autoSize: false);
        _errorLabel.Dock = DockStyle.Fill;
        root.Controls.Add(_errorLabel, 0, 3);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 6, 0, 0)
        };
        var closeButton = Ui.Outline("Fermer", 100, 40);
        closeButton.Click += (_, _) => Close();
        footer.Controls.Add(closeButton);

        if (_isAdmin)
        {
            var createButton = Ui.Primary("Créer un compte", 160, 40);
            createButton.Margin = new Padding(0, 0, 10, 0);
            createButton.Click += OnCreateUserClicked;
            footer.Controls.Add(createButton);
        }

        root.Controls.Add(footer, 0, 4);

        Controls.Add(root);
        CancelButton = closeButton;

        RebuildRows();
        Localization.Apply(this);
    }

    private void RebuildRows()
    {
        _listPanel.SuspendLayout();
        _listPanel.Controls.Clear();

        var filter = _searchBox.Text.Trim();
        var filtered = _users
            .Where(u => filter.Length == 0 ||
                        u.Username.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        u.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        u.Email.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(u => u.IsActive)
            .ThenBy(u => u.DisplayName)
            .ToList();

        foreach (var user in filtered)
            _listPanel.Controls.Add(BuildUserRow(user));

        if (filtered.Count == 0)
        {
            var empty = Ui.Label(Localization.T("Aucun compte trouvé."), Palette.Small, Palette.Faint);
            empty.Margin = new Padding(4, 14, 0, 0);
            _listPanel.Controls.Add(empty);
        }

        _subtitle.Text = Localization.F("{0} compte(s) · {1} actif(s)",
            _users.Count, _users.Count(u => u.IsActive));
        _listPanel.ResumeLayout();
        ResizeRows();
    }

    private Control BuildUserRow(UserDto user)
    {
        var row = new RoundedPanel
        {
            Height = 72,
            Width = Math.Max(260, _listPanel.ClientSize.Width - 20),
            FillColor = user.IsActive ? Palette.Surface : Palette.WithAlpha(Palette.Surface, 150),
            BorderColor = Palette.Border,
            CornerRadius = 10,
            Margin = new Padding(0, 0, 0, 8)
        };

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 300,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 20, 14, 0)
        };

        if (_isAdmin && user.Id != _currentUserId)
        {
            var manage = Ui.Outline("Gérer", 84, 30);
            manage.Margin = new Padding(8, 0, 0, 0);
            var menu = BuildAdminMenu(user);
            manage.ContextMenuStrip = menu;
            manage.Click += (_, _) => menu.Show(manage, new Point(0, manage.Height));
            right.Controls.Add(manage);
        }

        var state = Ui.Pill(user.IsActive ? Localization.T("Actif") : Localization.T("Inactif"),
            user.IsActive ? Palette.Success : Palette.Faint);
        state.Margin = new Padding(8, 0, 0, 0);
        right.Controls.Add(state);

        var role = Ui.Pill(user.IsAdmin ? Localization.T("Administrateur") : Localization.T("Utilisateur"),
            user.IsAdmin ? Palette.Warning : Palette.Accent);
        role.Margin = new Padding(8, 0, 0, 0);
        right.Controls.Add(role);

        row.Controls.Add(right);

        var avatar = new AvatarControl
        {
            DisplayName = user.DisplayName,
            Seed = user.Id.ToString(),
            ShowStatus = true,
            Online = user.IsActive,
            Location = new Point(14, 15),
            Size = new Size(42, 42)
        };
        row.Controls.Add(avatar);

        var nameText = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;
        if (user.Id == _currentUserId)
            nameText += Localization.T(" (vous)");

        var name = Ui.Label(nameText, Palette.BodyBold, user.IsActive ? Palette.Text : Palette.Muted);
        name.AutoSize = false;
        name.Tag = "left";
        name.Location = new Point(68, 13);
        name.Size = new Size(Math.Max(80, row.Width - 380), 20);
        row.Controls.Add(name);

        var detail = user.Email.Length > 0
            ? $"@{user.Username} · {user.Email}"
            : $"@{user.Username}";
        var subtitle = Ui.Label(detail, Palette.Small, Palette.Muted);
        subtitle.AutoSize = false;
        subtitle.Tag = "left";
        subtitle.Location = new Point(68, 36);
        subtitle.Size = new Size(Math.Max(80, row.Width - 380), 18);
        row.Controls.Add(subtitle);

        return row;
    }

    private void ResizeRows()
    {
        var width = Math.Max(260, _listPanel.ClientSize.Width - 20);
        foreach (Control control in _listPanel.Controls)
        {
            if (control is not RoundedPanel row)
                continue;

            row.Width = width;

            foreach (Control child in row.Controls)
            {
                if (child is Label { Tag: "left" } label)
                    label.Width = Math.Max(80, width - 380);
            }
        }
    }

    private ContextMenuStrip BuildAdminMenu(UserDto user)
    {
        var menu = new ContextMenuStrip();

        var roleItem = new ToolStripMenuItem(
            user.IsAdmin ? Localization.T("Rétrograder en utilisateur") : Localization.T("Promouvoir administrateur"));
        roleItem.Click += async (_, _) => await UpdateRoleAsync(user);
        menu.Items.Add(roleItem);

        menu.Items.Add(Localization.T("Réinitialiser le mot de passe"), null,
            (_, _) => ResetPassword(user));

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(
            user.IsActive ? Localization.T("Désactiver le compte") : Localization.T("Réactiver le compte"),
            null,
            async (_, _) => await ToggleActiveAsync(user));

        menu.Items.Add(new ToolStripSeparator());

        var deleteItem = new ToolStripMenuItem(Localization.T("Supprimer le compte"))
        {
            ForeColor = Palette.Danger
        };
        deleteItem.Click += async (_, _) => await DeleteUserAsync(user);
        menu.Items.Add(deleteItem);

        return menu;
    }

    private async void OnCreateUserClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;

        using var dialog = new CreateUserForm(_api);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _errorLabel.Text = string.Empty;
        try
        {
            await ReloadUsersAsync();
        }
        catch (Exception ex)
        {
            _errorLabel.Text = Ui.FriendlyError(ex);
        }
    }

    private async Task UpdateRoleAsync(UserDto user)
    {
        if (_busy)
            return;

        var newRole = user.IsAdmin ? "User" : "OrgAdmin";
        var verb = user.IsAdmin
            ? Localization.T("retirer le rôle administrateur à")
            : Localization.T("promouvoir administrateur");
        var confirm = MessageBox.Show(this,
            Localization.F("Voulez-vous {0} {1} (@{2}) ?", verb, user.DisplayName, user.Username),
            Localization.T("Changer le rôle"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
            return;

        await RunAdminActionAsync(async () =>
        {
            await _api.UpdateUserRoleAsync(user.Id, new UpdateUserRoleRequest { Role = newRole });
            _errorLabel.Text = string.Empty;
        });
    }

    private void ResetPassword(UserDto user)
    {
        if (_busy)
            return;

        using var dialog = new ChangePasswordForm(_api, user.Id, user.Username, adminReset: true);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _errorLabel.Text = string.Empty;
        MessageBox.Show(this,
            Localization.F("Le mot de passe de @{0} a été réinitialisé.", user.Username),
            Localization.T("Mot de passe réinitialisé"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private async Task DeleteUserAsync(UserDto user)
    {
        if (_busy)
            return;

        var confirm = MessageBox.Show(this,
            Localization.F("Supprimer le compte de {0} (@{1}) ?\n\nLe compte sera anonymisé et ne pourra plus se connecter. Les messages existants sont conservés.",
                user.DisplayName, user.Username),
            Localization.T("Supprimer le compte"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
            return;

        await RunAdminActionAsync(async () =>
        {
            await _api.DeleteUserAsync(user.Id);
            _errorLabel.Text = string.Empty;
        });
    }

    private async Task ToggleActiveAsync(UserDto user)
    {
        if (_busy)
            return;

        var verb = user.IsActive ? Localization.T("désactiver") : Localization.T("réactiver");
        var confirm = MessageBox.Show(this,
            Localization.F("Voulez-vous {0} le compte de {1} (@{2}) ?", verb, user.DisplayName, user.Username),
            Localization.T("Confirmer"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
            return;

        await RunAdminActionAsync(async () =>
        {
            if (user.IsActive)
                await _api.DeactivateUserAsync(user.Id);
            else
                await _api.ReactivateUserAsync(user.Id);

            _errorLabel.Text = string.Empty;
        });
    }

    private async Task RunAdminActionAsync(Func<Task> action)
    {
        _busy = true;
        UseWaitCursor = true;
        _errorLabel.Text = string.Empty;

        try
        {
            await action();
            await ReloadUsersAsync();
        }
        catch (Exception ex)
        {
            _errorLabel.Text = Ui.FriendlyError(ex);
        }
        finally
        {
            _busy = false;
            UseWaitCursor = false;
        }
    }

    private async Task ReloadUsersAsync()
    {
        var users = await _api.GetUsersAsync();
        _users.Clear();
        _users.AddRange(users);
        RebuildRows();
    }
}
