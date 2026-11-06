using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>Gestion des participants d'une conversation.</summary>
public sealed class GroupMembersForm : Form
{
    private readonly ApiClient _api;
    private readonly int _currentUserId;
    private readonly List<UserDto> _allUsers;

    private readonly SmoothFlowPanel _listPanel;
    private readonly Panel _addPanel;
    private readonly ComboBox _userCombo;
    private readonly CheckBox _adminCheck;
    private readonly RoundedButton _addButton;
    private readonly Label _errorLabel;
    private readonly Label _subtitle;
    private readonly RowStyle _addRowStyle;

    private ConversationDto _conversation;
    private bool _busy;

    public GroupMembersForm(ApiClient api, ConversationDto conversation, int currentUserId, IEnumerable<UserDto> users)
    {
        _api = api;
        _conversation = conversation;
        _currentUserId = currentUserId;
        _allUsers = users.Where(u => u.IsActive && u.Id != currentUserId).ToList();

        Ui.StyleDialog(this);
        Text = conversation.IsGroup ? "Membres du groupe" : "Participants";
        ClientSize = new Size(640, 620);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(24),
            BackColor = Palette.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _addRowStyle = new RowStyle(SizeType.Absolute, 104);
        root.RowStyles.Add(_addRowStyle);
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var title = Ui.Label(conversation.IsGroup ? conversation.Title ?? "Groupe" : "Participants",
            new Font("Segoe UI", 16f, FontStyle.Bold));
        title.Location = new Point(0, 0);
        _subtitle = Ui.Label(string.Empty, Palette.Small, Palette.Muted);
        _subtitle.Location = new Point(0, 30);
        header.Controls.Add(title);
        header.Controls.Add(_subtitle);
        root.Controls.Add(header, 0, 0);

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
        root.Controls.Add(_listPanel, 0, 1);

        _addPanel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            CornerRadius = 10,
            Padding = new Padding(14, 10, 14, 10)
        };
        var addCaption = Ui.Label("Ajouter un participant", Palette.SmallBold, Palette.Muted);
        addCaption.Location = new Point(14, 10);
        _addPanel.Controls.Add(addCaption);

        _userCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Palette.SurfaceAlt,
            ForeColor = Palette.Text,
            Font = Palette.Body,
            Location = new Point(14, 34),
            Width = 300,
            Height = 30
        };
        _addPanel.Controls.Add(_userCombo);

        _adminCheck = new CheckBox
        {
            Text = "Admin du groupe",
            ForeColor = Palette.Muted,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Font = Palette.Small,
            Location = new Point(326, 36),
            AutoSize = true
        };
        _addPanel.Controls.Add(_adminCheck);

        _addButton = Ui.Primary("Ajouter", 92, 32);
        _addButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _addButton.Location = new Point(_addPanel.Width - 106, 32);
        _addPanel.Resize += (_, _) => _addButton.Left = _addPanel.Width - 106;
        _addButton.Click += OnAddClicked;
        _addPanel.Controls.Add(_addButton);

        root.Controls.Add(_addPanel, 0, 2);

        _errorLabel = Ui.Label(string.Empty, Palette.Small, Palette.Danger, autoSize: false);
        _errorLabel.Dock = DockStyle.Fill;
        root.Controls.Add(_errorLabel, 0, 3);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 8, 0, 0)
        };
        var closeButton = Ui.Outline("Fermer", 100, 40);
        closeButton.Click += (_, _) => Close();
        footer.Controls.Add(closeButton);
        root.Controls.Add(footer, 0, 4);

        Controls.Add(root);
        CancelButton = closeButton;
        Localization.Apply(this);

        Shown += async (_, _) => await ReloadAsync();
        BuildRows();
        UpdateVisibility();
    }

    public ConversationDto Conversation => _conversation;

    private bool IsCurrentUserAdmin => _conversation.Participants
        .Any(p => p.UserId == _currentUserId && p.IsGroupAdmin);

    private bool CanManage => _conversation.IsGroup && IsCurrentUserAdmin;

    private void UpdateVisibility()
    {
        _addPanel.Visible = CanManage;
        _addRowStyle.Height = CanManage ? 104 : 0;
    }

    private async Task ReloadAsync()
    {
        try
        {
            var fresh = await _api.GetConversationAsync(_conversation.Id);
            _conversation = fresh;
            BuildRows();
            UpdateVisibility();
        }
        catch (Exception ex)
        {
            ShowError(Ui.FriendlyError(ex));
        }
    }

    private void BuildRows()
    {
        _listPanel.SuspendLayout();
        _listPanel.Controls.Clear();

        var participants = _conversation.Participants
            .OrderByDescending(p => p.IsGroupAdmin)
            .ThenBy(p => p.DisplayName)
            .ToList();

        foreach (var participant in participants)
            _listPanel.Controls.Add(BuildParticipantRow(participant));

        if (participants.Count == 0)
        {
            var empty = Ui.Label(Localization.T("Aucun participant."), Palette.Small, Palette.Faint);
            empty.Margin = new Padding(4, 12, 0, 0);
            _listPanel.Controls.Add(empty);
        }

        _subtitle.Text = _conversation.IsGroup
            ? Localization.F("{0} membres · {1}", participants.Count,
                _conversation.ParticipantSummary(_currentUserId))
            : Localization.T("Conversation privée entre deux participants");

        _listPanel.ResumeLayout();
        ResizeRows();
        RefreshAddCombo();
    }

    private Control BuildParticipantRow(ConversationParticipantDto participant)
    {
        var row = new RoundedPanel
        {
            Height = 62,
            Width = Math.Max(220, _listPanel.ClientSize.Width - 20),
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            CornerRadius = 9,
            Margin = new Padding(0, 0, 0, 8)
        };

        var avatar = new AvatarControl
        {
            DisplayName = participant.DisplayName,
            Seed = participant.UserId.ToString(),
            Location = new Point(12, 11),
            Size = new Size(40, 40)
        };
        row.Controls.Add(avatar);

        var name = Ui.Label(string.IsNullOrWhiteSpace(participant.DisplayName)
            ? participant.Username
            : participant.DisplayName, Palette.BodyBold);
        name.Location = new Point(64, 10);
        row.Controls.Add(name);

        var handle = Ui.Label("@" + participant.Username, Palette.Small, Palette.Muted);
        handle.Location = new Point(64, 31);
        row.Controls.Add(handle);

        if (participant.UserId == _currentUserId)
        {
            var me = Ui.Pill(Localization.T("vous"), Palette.Accent);
            me.Location = new Point(64 + handle.PreferredWidth + 10, 31);
            row.Controls.Add(me);
        }

        if (participant.IsGroupAdmin)
        {
            var badge = Ui.Pill(Localization.T("admin"), Palette.Warning);
            badge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            badge.Location = new Point(row.Width - badge.PreferredWidth - 16, 12);
            row.Controls.Add(badge);
        }

        var canRemove = CanManage &&
                        _conversation.Participants.Count > 2 &&
                        participant.UserId != _currentUserId;

        if (canRemove)
        {
            var remove = Ui.Ghost(Localization.T("Retirer"), 70, 30);
            remove.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            remove.Location = new Point(row.Width - 86, 16);
            remove.Click += async (_, _) => await RemoveParticipantAsync(participant);
            row.Controls.Add(remove);
        }

        return row;
    }

    private void ResizeRows()
    {
        var width = Math.Max(220, _listPanel.ClientSize.Width - 20);
        foreach (Control control in _listPanel.Controls)
        {
            if (control is not RoundedPanel row)
                continue;

            row.Width = width;

            foreach (Control child in row.Controls)
            {
                if (child is RoundedButton button)
                    button.Left = row.Width - button.Width - 16;
                else if (child is Label { Text: "admin" } badge)
                    badge.Left = row.Width - badge.PreferredWidth - 16;
            }
        }
    }

    private void RefreshAddCombo()
    {
        if (!CanManage)
            return;

        var participants = _conversation.Participants.Select(p => p.UserId).ToHashSet();
        var options = _allUsers
            .Where(u => !participants.Contains(u.Id))
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserOption(u.Id, $"{u.DisplayName} (@{u.Username})"))
            .ToList();

        _userCombo.DataSource = null;
        _userCombo.DataSource = options;
        _addButton.Enabled = options.Count > 0;
        _adminCheck.Enabled = options.Count > 0;
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        if (_busy || _userCombo.SelectedItem is not UserOption option)
            return;

        _busy = true;
        SetBusy(true);
        ShowError(string.Empty);

        try
        {
            await _api.AddParticipantAsync(_conversation.Id, new AddParticipantRequest
            {
                UserId = option.Id,
                IsGroupAdmin = _adminCheck.Checked
            });

            _adminCheck.Checked = false;
            await ReloadAsync();
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

    private async Task RemoveParticipantAsync(ConversationParticipantDto participant)
    {
        if (_busy)
            return;

        var name = string.IsNullOrWhiteSpace(participant.DisplayName) ? participant.Username : participant.DisplayName;
        var confirm = MessageBox.Show(this,
            Localization.F("Retirer {0} de cette conversation ?", name),
            Localization.T("Confirmer"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
            return;

        _busy = true;
        SetBusy(true);
        ShowError(string.Empty);

        try
        {
            await _api.RemoveParticipantAsync(_conversation.Id, participant.UserId);
            await ReloadAsync();
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
        _addButton.Enabled = !busy && _userCombo.Items.Count > 0;
        _userCombo.Enabled = !busy;
        _adminCheck.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void ShowError(string message) => _errorLabel.Text = message;

    private sealed record UserOption(int Id, string Label)
    {
        public override string ToString() => Label;
    }
}
