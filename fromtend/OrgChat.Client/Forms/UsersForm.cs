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

        var searchField = Ui.Field("Rechercher un compte…", out _searchBox, height: 40);
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
        root.Controls.Add(footer, 0, 4);

        Controls.Add(root);
        CancelButton = closeButton;

        RebuildRows();
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
            var empty = Ui.Label("Aucun compte trouvé.", Palette.Small, Palette.Faint);
            empty.Margin = new Padding(4, 14, 0, 0);
            _listPanel.Controls.Add(empty);
        }

        _subtitle.Text = $"{_users.Count} compte(s) · {_users.Count(u => u.IsActive)} actif(s)";
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
            var action = user.IsActive
                ? Ui.Danger("Désactiver", 96, 30)
                : Ui.Outline("Réactiver", 96, 30);

            action.Margin = new Padding(8, 0, 0, 0);
            action.Click += async (_, _) => await ToggleActiveAsync(user);
            right.Controls.Add(action);
        }

        var state = Ui.Pill(user.IsActive ? "actif" : "inactif",
            user.IsActive ? Palette.Success : Palette.Faint);
        state.Margin = new Padding(8, 0, 0, 0);
        right.Controls.Add(state);

        var role = Ui.Pill(user.IsAdmin ? "admin" : "utilisateur",
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
            nameText += " (vous)";

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

    private async Task ToggleActiveAsync(UserDto user)
    {
        if (_busy)
            return;

        var verb = user.IsActive ? "désactiver" : "réactiver";
        var confirm = MessageBox.Show(this,
            $"Voulez-vous {verb} le compte de {user.DisplayName} (@{user.Username}) ?",
            "Confirmer",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
            return;

        _busy = true;
        UseWaitCursor = true;
        _errorLabel.Text = string.Empty;

        try
        {
            if (user.IsActive)
                await _api.DeactivateUserAsync(user.Id);
            else
                await _api.ReactivateUserAsync(user.Id);

            user.IsActive = !user.IsActive;
            RebuildRows();
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
}
