using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>Création d'une conversation privée ou d'un groupe.</summary>
public sealed class NewConversationForm : Form
{
    private readonly ApiClient _api;
    private readonly int _currentUserId;
    private readonly List<UserDto> _users;
    private readonly List<UserSelectRow> _rows = new();
    private readonly HashSet<int> _selectedGroupIds = new();
    private int? _selectedPrivateId;

    private readonly RoundedButton _privateButton;
    private readonly RoundedButton _groupButton;
    private readonly Panel _titleRow;
    private readonly TextBox _titleBox;
    private readonly TextBox _searchBox;
    private readonly SmoothFlowPanel _usersPanel;
    private readonly Label _hintLabel;
    private readonly Label _errorLabel;
    private readonly RoundedButton _createButton;
    private readonly RoundedButton _cancelButton;

    private bool _privateMode = true;
    private bool _busy;

    public NewConversationForm(ApiClient api, IEnumerable<UserDto> users, int currentUserId)
    {
        _api = api;
        _currentUserId = currentUserId;
        _users = users
            .Where(u => u.Id != currentUserId && u.IsActive)
            .OrderBy(u => u.DisplayName)
            .ToList();

        Ui.StyleDialog(this);
        Text = "Nouvelle conversation";
        ClientSize = new Size(620, 680);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(24),
            BackColor = Palette.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var title = Ui.Label("Nouvelle conversation", new Font("Segoe UI", 16f, FontStyle.Bold));
        title.Location = new Point(0, 0);
        var subtitle = Ui.Label("Choisissez un collègue ou créez un groupe de discussion.", Palette.Small, Palette.Muted);
        subtitle.Location = new Point(0, 30);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        root.Controls.Add(header, 0, 0);

        var typePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = Padding.Empty
        };
        _privateButton = Ui.Primary("Discussion privée", 168, 38);
        _privateButton.Margin = new Padding(0, 0, 10, 0);
        _privateButton.Click += (_, _) => SetMode(true);
        _groupButton = Ui.Outline("Groupe", 130, 38);
        _groupButton.Click += (_, _) => SetMode(false);
        typePanel.Controls.Add(_privateButton);
        typePanel.Controls.Add(_groupButton);
        root.Controls.Add(typePanel, 0, 1);

        _titleRow = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background, Visible = false };
        var titleCaption = Ui.Label("Nom du groupe", Palette.Small, Palette.Muted);
        titleCaption.Location = new Point(0, 0);
        var titleField = Ui.Field("Ex. Équipe Réseau", out _titleBox, height: 40);
        titleField.Location = new Point(0, 20);
        titleField.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        _titleRow.Resize += (_, _) => titleField.Width = _titleRow.Width;
        _titleRow.Controls.Add(titleCaption);
        _titleRow.Controls.Add(titleField);
        root.Controls.Add(_titleRow, 0, 2);

        var searchField = Ui.Field("Rechercher un collègue…", out _searchBox, height: 40);
        searchField.Dock = DockStyle.Top;
        _searchBox.TextChanged += (_, _) => RebuildRows();
        root.Controls.Add(searchField, 0, 3);

        _hintLabel = Ui.Label("Sélectionnez un collègue", Palette.Small, Palette.Faint);
        root.Controls.Add(_hintLabel, 0, 4);

        _usersPanel = new SmoothFlowPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Palette.Background,
            Padding = new Padding(0, 2, 0, 2)
        };
        _usersPanel.Resize += (_, _) => ResizeRows();
        root.Controls.Add(_usersPanel, 0, 5);

        _errorLabel = Ui.Label(string.Empty, Palette.Small, Palette.Danger, autoSize: false);
        _errorLabel.Dock = DockStyle.Fill;
        root.Controls.Add(_errorLabel, 0, 6);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 10, 0, 0)
        };
        _createButton = Ui.Primary("Créer", 130, 42);
        _createButton.Click += OnCreateClicked;
        _cancelButton = Ui.Outline("Annuler", 110, 42);
        _cancelButton.Margin = new Padding(0, 0, 10, 0);
        _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        footer.Controls.Add(_createButton);
        footer.Controls.Add(_cancelButton);
        root.Controls.Add(footer, 0, 7);

        Controls.Add(root);
        CancelButton = _cancelButton;

        RebuildRows();
        UpdateModeVisuals();
        Localization.Apply(this);
    }

    public ConversationDto? CreatedConversation { get; private set; }

    private void SetMode(bool privateMode)
    {
        if (_busy || _privateMode == privateMode)
            return;

        _privateMode = privateMode;
        _selectedGroupIds.Clear();
        _selectedPrivateId = null;
        _titleRow.Visible = !privateMode;
        _errorLabel.Text = string.Empty;
        UpdateModeVisuals();
        RebuildRows();
    }

    private void UpdateModeVisuals()
    {
        ApplyToggle(_privateButton, _privateMode);
        ApplyToggle(_groupButton, !_privateMode);
        _hintLabel.Text = Localization.T(_privateMode
            ? "Sélectionnez un collègue pour démarrer une discussion privée."
            : "Sélectionnez au moins un collègue. Le groupe inclura votre compte.");
    }

    private static void ApplyToggle(RoundedButton button, bool active)
    {
        button.Outline = !active;
        button.ForeColor = active ? Palette.OnAccent : Palette.Muted;
        button.Invalidate();
    }

    private void RebuildRows()
    {
        _usersPanel.SuspendLayout();
        _usersPanel.Controls.Clear();
        _rows.Clear();

        var filter = _searchBox.Text.Trim();
        var visibleUsers = _users
            .Where(u => filter.Length == 0 ||
                        u.Username.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        u.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase));

        foreach (var user in visibleUsers)
        {
            var row = new UserSelectRow(user, _privateMode)
            {
                Width = Math.Max(200, _usersPanel.ClientSize.Width - 20),
                Checked = _privateMode
                    ? _selectedPrivateId == user.Id
                    : _selectedGroupIds.Contains(user.Id)
            };
            row.CheckedChanged += OnRowChecked;
            _rows.Add(row);
            _usersPanel.Controls.Add(row);
        }

        if (_rows.Count == 0)
        {
            var empty = Ui.Label(Localization.T("Aucun collègue trouvé."), Palette.Small, Palette.Faint);
            empty.Margin = new Padding(4, 14, 0, 0);
            _usersPanel.Controls.Add(empty);
        }

        _usersPanel.ResumeLayout();
    }

    private void ResizeRows()
    {
        var width = Math.Max(200, _usersPanel.ClientSize.Width - 20);
        foreach (var row in _rows)
            row.Width = width;
    }

    private void OnRowChecked(object? sender, EventArgs e)
    {
        if (sender is not UserSelectRow row)
            return;

        if (_privateMode)
        {
            if (row.Checked)
            {
                _selectedPrivateId = row.User.Id;
                foreach (var other in _rows.Where(r => r != row && r.Checked))
                    other.Checked = false;
            }
            else if (_selectedPrivateId == row.User.Id)
            {
                _selectedPrivateId = null;
            }
        }
        else
        {
            if (row.Checked)
                _selectedGroupIds.Add(row.User.Id);
            else
                _selectedGroupIds.Remove(row.User.Id);
        }

        if (!string.IsNullOrWhiteSpace(_errorLabel.Text))
            _errorLabel.Text = string.Empty;
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;

        var request = new ConversationCreateRequest();
        if (_privateMode)
        {
            if (_selectedPrivateId is null)
            {
                ShowError(Localization.T("Sélectionnez le collègue avec qui discuter."));
                return;
            }

            request.Type = "Private";
            request.ParticipantUserIds.Add(_selectedPrivateId.Value);
        }
        else
        {
            var title = _titleBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                ShowError(Localization.T("Donnez un nom au groupe."));
                _titleBox.Focus();
                return;
            }

            if (_selectedGroupIds.Count == 0)
            {
                ShowError(Localization.T("Sélectionnez au moins un collègue pour le groupe."));
                return;
            }

            request.Type = "Group";
            request.Title = title;
            request.ParticipantUserIds.AddRange(_selectedGroupIds);
        }

        _busy = true;
        SetBusy(true);
        ShowError(string.Empty);

        try
        {
            CreatedConversation = await _api.CreateConversationAsync(request);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            ShowError(Ui.FriendlyError(ex));
        }
        finally
        {
            _busy = false;
            if (!IsDisposed)
                SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _createButton.Enabled = !busy;
        _cancelButton.Enabled = !busy;
        _createButton.Text = Localization.T(busy ? "Création…" : "Créer");

        foreach (var row in _rows)
            row.Enabled = !busy;

        UseWaitCursor = busy;
    }

    private void ShowError(string message)
    {
        _errorLabel.Text = message;
    }
}
