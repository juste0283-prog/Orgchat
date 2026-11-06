using System.Diagnostics;
using OrgChat.Client.Configuration;
using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>Fenêtre principale : liste des conversations et messagerie temps réel.</summary>
public sealed class MainForm : Form
{
    private readonly ApiClient _api;
    private readonly ClientOptions _options;
    private readonly AuthResponse _auth;
    private readonly RealtimeChatService _realtime;

    private List<UserDto> _users = new();
    private List<ConversationDto> _conversations = new();
    private ConversationDto? _current;
    private readonly List<MessageDto> _messages = new();
    private readonly HashSet<int> _messageIds = new();
    private readonly Dictionary<int, int> _unreadCounts = new();
    private readonly HashSet<int> _onlineUsers = new();
    private readonly Dictionary<int, string> _typingUsers = new();
    private readonly Dictionary<int, DateTime> _typingExpirations = new();
    private readonly HashSet<int> _toastedMessageIds = new();
    private readonly System.Windows.Forms.Timer _markReadTimer = new() { Interval = 450 };
    private readonly System.Windows.Forms.Timer _typingTimer = new() { Interval = 2600 };
    private readonly System.Windows.Forms.Timer _typingUiTimer = new() { Interval = 1000 };
    private bool _markingRead;
    private bool _typingSent;
    private int? _typingConversationId;
    private int _notificationUnreadCount;
    private int _page = 1;
    private bool _hasMore;
    private bool _loadingOlder;
    private bool _sending;
    private int _loadVersion;
    private CancellationTokenSource? _messageCts;

    private SmoothFlowPanel _conversationList = null!;
    private TextBox _searchBox = null!;
    private TextBox _composerBox = null!;
    private RoundedButton _sendButton = null!;
    private RoundedButton _attachButton = null!;
    private RoundedButton _membersButton = null!;
    private RoundedButton _refreshButton = null!;
    private RoundedButton _notificationsButton = null!;
    private SmoothFlowPanel _messagesPanel = null!;
    private Panel _emptyState = null!;
    private Label _emptyTitle = null!;
    private Label _emptySubtitle = null!;
    private Label _titleLabel = null!;
    private Label _subtitleLabel = null!;
    private Label _statusLabel = null!;
    private Label _meNameLabel = null!;
    private Label _meHandleLabel = null!;
    private Label _composerError = null!;
    private LinkLabel _loadOlderLink = null!;
    private AvatarControl _headerAvatar = null!;
    private AvatarControl _meAvatar = null!;
    private Panel _headerPanel = null!;
    private NotifyIcon _notifyIcon = null!;
    private int? _pendingNotificationConversationId;
    private bool _allowClose;

    public MainForm(ApiClient api, ClientOptions options, AuthResponse auth)
    {
        _api = api;
        _options = options;
        _auth = auth;
        _realtime = new RealtimeChatService(api);

        Ui.StyleForm(this);
        Text = "OrgChat";
        ClientSize = new Size(1280, 800);
        MinimumSize = new Size(1024, 680);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Palette.Background;
        ForeColor = Palette.Text;
        DoubleBuffered = true;

        BuildUi();
        BuildNotifications();
        WireRealtime();
        _markReadTimer.Tick += OnMarkReadTimerTick;
        _typingTimer.Tick += async (_, _) => await SetTypingAsync(false);
        _typingUiTimer.Tick += (_, _) => ExpireTypingUsers();
        _typingUiTimer.Start();
        UpdateUnreadIndicators();
        UpdateNotificationBadge();

        Shown += async (_, _) => await InitializeAsync();
    }

    public event EventHandler? LogoutRequested;

    // ------------------------------------------------------------------ UI

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Palette.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(BuildMainArea(), 1, 0);

        Controls.Add(root);
    }

    private Control BuildSidebar()
    {
        var sidebar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Palette.Sidebar,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));

        var brand = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Sidebar };
        var mark = new BrandMark { Location = new Point(18, 22), Size = new Size(40, 40) };
        brand.Controls.Add(mark);

        var brandName = Ui.Label("OrgChat", new Font("Segoe UI", 15f, FontStyle.Bold));
        brandName.Location = new Point(68, 20);
        brand.Controls.Add(brandName);

        _statusLabel = Ui.Label("● Hors ligne", Palette.Small, Palette.Danger);
        _statusLabel.Location = new Point(69, 44);
        brand.Controls.Add(_statusLabel);

        sidebar.Controls.Add(brand, 0, 0);

        var searchHost = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Sidebar, Padding = new Padding(14, 6, 14, 6) };
        var searchField = Ui.Field("Rechercher une conversation… (Entrée : messages)", out _searchBox, height: 38);
        searchField.Dock = DockStyle.Fill;
        _searchBox.TextChanged += (_, _) => RebuildConversationList();
        _searchBox.KeyDown += OnSearchKeyDown;
        searchHost.Controls.Add(searchField);
        sidebar.Controls.Add(searchHost, 0, 1);

        var newHost = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Sidebar, Padding = new Padding(14, 4, 14, 8) };
        var newButton = Ui.Primary("+  Nouvelle conversation", 300, 42);
        newButton.Dock = DockStyle.Fill;
        newButton.Click += OnNewConversationClicked;
        newHost.Controls.Add(newButton);
        sidebar.Controls.Add(newHost, 0, 2);

        _conversationList = new SmoothFlowPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Palette.Sidebar,
            Padding = new Padding(0, 4, 0, 4)
        };
        _conversationList.Resize += (_, _) => ResizeConversationItems();
        sidebar.Controls.Add(_conversationList, 0, 3);

        sidebar.Controls.Add(BuildUserCard(), 0, 4);

        return sidebar;
    }

    private Control BuildUserCard()
    {
        var card = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Sidebar };
        var divider = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Palette.Border };
        card.Controls.Add(divider);

        _meAvatar = new AvatarControl
        {
            Location = new Point(16, 18),
            Size = new Size(44, 44)
        };
        card.Controls.Add(_meAvatar);

        _meNameLabel = Ui.Label(string.Empty, Palette.BodyBold);
        _meNameLabel.Location = new Point(70, 14);
        card.Controls.Add(_meNameLabel);

        _meHandleLabel = Ui.Label(string.Empty, Palette.Small, Palette.Muted);
        _meHandleLabel.Location = new Point(70, 34);
        card.Controls.Add(_meHandleLabel);

        var actions = new FlowLayoutPanel
        {
            Location = new Point(66, 56),
            Width = 250,
            Height = 32,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Palette.Sidebar,
            Padding = Padding.Empty
        };

        var profile = Ui.Ghost("Profil", 64, 28);
        profile.Margin = new Padding(0, 0, 6, 0);
        profile.Click += OnProfileClicked;
        actions.Controls.Add(profile);

        var directory = Ui.Ghost("Annuaire", 74, 28);
        directory.Margin = new Padding(0, 0, 6, 0);
        directory.Click += OnDirectoryClicked;
        actions.Controls.Add(directory);

        var logout = Ui.Ghost("Déconnexion", 96, 28);
        logout.Click += OnLogoutClicked;
        actions.Controls.Add(logout);

        card.Controls.Add(actions);
        return card;
    }

    private Control BuildMainArea()
    {
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Palette.Background,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));

        main.Controls.Add(BuildHeader(), 0, 0);

        var olderHost = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        _loadOlderLink = new LinkLabel
        {
            Dock = DockStyle.Fill,
            Text = "Charger les messages plus anciens",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = Palette.Small,
            LinkColor = Palette.Accent,
            ActiveLinkColor = Palette.AccentHover,
            VisitedLinkColor = Palette.Accent,
            LinkBehavior = LinkBehavior.HoverUnderline,
            BackColor = Palette.Background,
            Visible = false
        };
        _loadOlderLink.LinkClicked += async (_, _) => await LoadOlderAsync();
        olderHost.Controls.Add(_loadOlderLink);
        main.Controls.Add(olderHost, 0, 1);

        var messagesHost = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };

        _messagesPanel = new SmoothFlowPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Palette.Background,
            Padding = new Padding(8, 10, 8, 10),
            Visible = false
        };
        _messagesPanel.Resize += (_, _) => ResizeMessageBubbles();
        messagesHost.Controls.Add(_messagesPanel);

        _emptyState = BuildEmptyState();
        messagesHost.Controls.Add(_emptyState);

        main.Controls.Add(messagesHost, 0, 2);
        main.Controls.Add(BuildComposer(), 0, 3);

        return main;
    }

    private Control BuildHeader()
    {
        _headerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };

        _headerAvatar = new AvatarControl
        {
            Location = new Point(20, 15),
            Size = new Size(44, 44),
            Visible = false
        };
        _headerPanel.Controls.Add(_headerAvatar);

        _titleLabel = Ui.Label("Bienvenue sur OrgChat", Palette.Title, Palette.Text, autoSize: false);
        _titleLabel.Location = new Point(76, 14);
        _titleLabel.Size = new Size(420, 24);
        _headerPanel.Controls.Add(_titleLabel);

        _subtitleLabel = Ui.Label("Sélectionnez une conversation pour commencer.", Palette.Small, Palette.Muted, autoSize: false);
        _subtitleLabel.Location = new Point(76, 38);
        _subtitleLabel.Size = new Size(420, 18);
        _headerPanel.Controls.Add(_subtitleLabel);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 370,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 17, 20, 0)
        };

        _refreshButton = Ui.Ghost("⟳", 38, 38);
        _refreshButton.Margin = new Padding(8, 0, 0, 0);
        _refreshButton.Click += async (_, _) => await RefreshAllAsync(_current?.Id);
        actions.Controls.Add(_refreshButton);

        _notificationsButton = Ui.Ghost("🔔", 54, 38);
        _notificationsButton.Margin = new Padding(8, 0, 0, 0);
        _notificationsButton.Click += OnNotificationsClicked;
        actions.Controls.Add(_notificationsButton);

        _membersButton = Ui.Outline("Membres", 96, 38);
        _membersButton.Margin = new Padding(8, 0, 0, 0);
        _membersButton.Visible = false;
        _membersButton.Click += OnMembersClicked;
        actions.Controls.Add(_membersButton);

        var directoryButton = Ui.Outline("Annuaire", 96, 38);
        directoryButton.Margin = new Padding(8, 0, 0, 0);
        directoryButton.Click += OnDirectoryClicked;
        actions.Controls.Add(directoryButton);

        _headerPanel.Controls.Add(actions);

        var border = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Palette.Border };
        _headerPanel.Controls.Add(border);

        _headerPanel.Resize += (_, _) =>
        {
            var width = Math.Max(120, _headerPanel.ClientSize.Width - 76 - 390);
            _titleLabel.Width = width;
            _subtitleLabel.Width = width;
        };

        return _headerPanel;
    }

    private Panel BuildEmptyState()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };

        var content = new Panel { Size = new Size(460, 240), BackColor = Palette.Background };
        panel.Controls.Add(content);

        var mark = new BrandMark { Location = new Point(191, 0), Size = new Size(78, 78) };
        content.Controls.Add(mark);

        _emptyTitle = Ui.Label("Bienvenue sur OrgChat", new Font("Segoe UI", 18f, FontStyle.Bold),
            Palette.Text, autoSize: false);
        _emptyTitle.TextAlign = ContentAlignment.MiddleCenter;
        _emptyTitle.Location = new Point(0, 96);
        _emptyTitle.Size = new Size(460, 32);
        content.Controls.Add(_emptyTitle);

        _emptySubtitle = Ui.Label("Créez une conversation privée ou un groupe pour démarrer.",
            Palette.Body, Palette.Muted, autoSize: false);
        _emptySubtitle.TextAlign = ContentAlignment.MiddleCenter;
        _emptySubtitle.Location = new Point(0, 134);
        _emptySubtitle.Size = new Size(460, 44);
        content.Controls.Add(_emptySubtitle);

        var hint = Ui.Label("Astuce : Entrée envoie un message, Maj+Entrée ajoute une ligne.",
            Palette.Small, Palette.Faint, autoSize: false);
        hint.TextAlign = ContentAlignment.MiddleCenter;
        hint.Location = new Point(0, 190);
        hint.Size = new Size(460, 22);
        content.Controls.Add(hint);

        panel.Resize += (_, _) =>
        {
            content.Left = Math.Max(0, (panel.ClientSize.Width - content.Width) / 2);
            content.Top = Math.Max(0, (panel.ClientSize.Height - content.Height) / 2 - 20);
        };

        return panel;
    }

    private Control BuildComposer()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Palette.Background,
            Padding = new Padding(18, 10, 18, 12)
        };

        _composerError = Ui.Label(string.Empty, Palette.Small, Palette.Danger, autoSize: false);
        _composerError.Dock = DockStyle.Bottom;
        _composerError.Height = 18;
        _composerError.TextAlign = ContentAlignment.MiddleLeft;
        host.Controls.Add(_composerError);

        var shell = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            CornerRadius = 12,
            Padding = new Padding(12, 8, 8, 8)
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Palette.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _attachButton = Ui.Ghost("📎", 36, 36);
        _attachButton.Dock = DockStyle.Fill;
        _attachButton.Margin = new Padding(0, 5, 6, 3);
        _attachButton.Enabled = false;
        _attachButton.Click += OnAttachClicked;
        grid.Controls.Add(_attachButton, 0, 0);

        _composerBox = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Palette.Surface,
            ForeColor = Palette.Text,
            Font = Palette.Body,
            Multiline = true,
            AcceptsReturn = true,
            ScrollBars = ScrollBars.Vertical,
            PlaceholderText = "Écrivez un message…",
            Margin = new Padding(0, 2, 8, 2),
            Enabled = false
        };
        _composerBox.KeyDown += OnComposerKeyDown;
        _composerBox.TextChanged += OnComposerTextChanged;
        grid.Controls.Add(_composerBox, 1, 0);

        _sendButton = Ui.Primary("➤", 46, 44);
        _sendButton.Dock = DockStyle.Fill;
        _sendButton.Margin = new Padding(4, 2, 0, 2);
        _sendButton.Enabled = false;
        _sendButton.Click += async (_, _) => await SendMessageAsync();
        grid.Controls.Add(_sendButton, 2, 0);

        shell.Controls.Add(grid);
        host.Controls.Add(shell);
        return host;
    }

    private void WireRealtime()
    {
        _realtime.MessageReceived += OnRealtimeMessage;
        _realtime.MessageUpdated += OnRealtimeMessageUpdated;
        _realtime.MessageDeleted += OnRealtimeMessageDeleted;
        _realtime.ConversationDeleted += OnRealtimeConversationDeleted;
        _realtime.ConversationUpdated += OnRealtimeConversationUpdated;
        _realtime.ConversationRead += OnConversationRead;
        _realtime.PresenceChanged += OnPresenceChanged;
        _realtime.UserTyping += OnUserTyping;
        _realtime.NotificationReceived += OnNotificationReceived;
        _realtime.StateChanged += OnRealtimeStateChanged;
        _realtime.Reconnected += OnRealtimeReconnected;
    }

    private void BuildNotifications()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Ouvrir OrgChat", null, (_, _) => RestoreFromNotification());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quitter", null, (_, _) =>
        {
            _allowClose = true;
            _notifyIcon.Visible = false;
            Close();
        });

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "OrgChat",
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.BalloonTipClicked += (_, _) => RestoreFromNotification();
        _notifyIcon.DoubleClick += (_, _) => RestoreFromNotification();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        // Les messages de la conversation affichée sont considérés comme lus
        // dès que la fenêtre redevient active.
        if (_current is not null)
            ScheduleMarkRead();
    }

    // ------------------------------------------------------------- Lifecycle

    private async Task InitializeAsync()
    {
        UpdateUserCard();
        SetConnectionState(RealtimeState.Connecting);

        try
        {
            await _realtime.ConnectAsync();
        }
        catch
        {
            SetConnectionState(RealtimeState.Disconnected);
        }

        try
        {
            await LoadProfileAsync();
            await RefreshAllAsync(null);
            await RefreshPresenceAsync();
            await RefreshNotificationCountAsync();
        }
        catch (Exception ex)
        {
            ShowComposerError(Ui.FriendlyError(ex));
            ShowEmptyState("Connexion impossible",
                "Vérifiez que le serveur OrgChat est démarré et joignable, puis utilisez le bouton ⟳.");
        }
    }

    private async Task LoadProfileAsync()
    {
        try
        {
            var me = await _api.GetMeAsync();
            _auth.DisplayName = me.DisplayName;
            _auth.Email = me.Email;
            _auth.Role = me.Role;
            UpdateUserCard();
        }
        catch
        {
            // Le profil local reste utilisable.
        }
    }

    private void UpdateUserCard()
    {
        var display = string.IsNullOrWhiteSpace(_auth.DisplayName) ? _auth.Username : _auth.DisplayName;
        _meNameLabel.Text = display;
        _meHandleLabel.Text = "@" + _auth.Username + (_auth.IsAdmin ? " · admin" : string.Empty);
        _meAvatar.DisplayName = display;
        _meAvatar.Seed = _auth.UserId.ToString();
        _meAvatar.Invalidate();
    }

    private async Task RefreshAllAsync(int? selectConversationId)
    {
        _refreshButton.Enabled = false;
        try
        {
            _users = await _api.GetUsersAsync();
            _conversations = await _api.GetConversationsAsync();

            var conversationIds = _conversations.Select(c => c.Id).ToHashSet();
            foreach (var staleId in _unreadCounts.Keys.Where(id => !conversationIds.Contains(id)).ToList())
                _unreadCounts.Remove(staleId);
            foreach (var conversation in _conversations)
                _unreadCounts[conversation.Id] = conversation.UnreadCount;
            UpdateUnreadIndicators();

            RebuildConversationList();

            var target = selectConversationId is not null
                ? _conversations.FirstOrDefault(c => c.Id == selectConversationId)
                : _conversations.FirstOrDefault();

            if (target is not null)
                await SelectConversationAsync(target, force: true);
            else
                ShowEmptyState("Aucune conversation", "Créez une conversation privée ou un groupe pour démarrer.");
        }
        catch (Exception ex)
        {
            ShowComposerError(Ui.FriendlyError(ex));
        }
        finally
        {
            _refreshButton.Enabled = true;
        }
    }

    // ---------------------------------------------------------- Conversations

    private void RebuildConversationList()
    {
        _conversationList.SuspendLayout();
        _conversationList.Controls.Clear();

        var filter = _searchBox.Text.Trim();
        var conversations = _conversations
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .Where(c => filter.Length == 0 ||
                        c.DisplayTitle(_auth.UserId).Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        c.Participants.Any(p => p.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                                p.Username.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var conversation in conversations)
        {
            var item = new ConversationItem(conversation, _auth.UserId)
            {
                Selected = _current?.Id == conversation.Id,
                UnreadCount = _unreadCounts.GetValueOrDefault(conversation.Id),
                Online = IsConversationOnline(conversation)
            };
            item.Activate += async (_, _) => await SelectConversationAsync(conversation);
            var menu = BuildConversationMenu(conversation);
            if (menu is not null)
                item.ContextMenuStrip = menu;
            _conversationList.Controls.Add(item);
        }

        if (conversations.Count == 0)
        {
            var empty = Ui.Label(
                _conversations.Count == 0 ? "Aucune conversation." : "Aucun résultat.",
                Palette.Small,
                Palette.Faint);
            empty.Margin = new Padding(20, 16, 0, 0);
            _conversationList.Controls.Add(empty);
        }

        _conversationList.ResumeLayout();
        ResizeConversationItems();
    }

    private void ResizeConversationItems()
    {
        var width = Math.Max(220, _conversationList.ClientSize.Width - 22);
        foreach (Control control in _conversationList.Controls)
        {
            if (control is ConversationItem item)
                item.Width = width;
        }
    }

    private ContextMenuStrip? BuildConversationMenu(ConversationDto conversation)
    {
        var me = conversation.Participants.FirstOrDefault(p => p.UserId == _auth.UserId);
        var canLeave = conversation.IsGroup && me is not null;
        var canDelete = !conversation.IsGroup ||
                        me?.IsGroupAdmin == true ||
                        conversation.CreatedById == _auth.UserId;

        if (!canLeave && !canDelete)
            return null;

        var menu = new ContextMenuStrip();
        if (canLeave)
        {
            menu.Items.Add("Quitter le groupe", null,
                async (_, _) => await LeaveConversationAsync(conversation));
        }

        if (canDelete)
        {
            if (menu.Items.Count > 0)
                menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Supprimer la conversation", null,
                async (_, _) => await DeleteConversationAsync(conversation));
        }

        return menu;
    }

    private async Task LeaveConversationAsync(ConversationDto conversation)
    {
        var title = conversation.DisplayTitle(_auth.UserId);
        var confirm = MessageBox.Show(this,
            $"Quitter « {title} » ? Vous ne recevrez plus ses messages.",
            "Quitter le groupe",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
            return;

        try
        {
            await _api.LeaveConversationAsync(conversation.Id);
            await _realtime.LeaveConversationAsync(conversation.Id);
            RemoveConversationLocally(conversation.Id);
            await RefreshAllAsync(null);
        }
        catch (Exception ex)
        {
            ShowComposerError(Ui.FriendlyError(ex));
        }
    }

    private async Task DeleteConversationAsync(ConversationDto conversation)
    {
        var title = conversation.DisplayTitle(_auth.UserId);
        var confirm = MessageBox.Show(this,
            $"Supprimer définitivement « {title} » pour tous les participants ?",
            "Supprimer la conversation",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
            return;

        try
        {
            await _api.DeleteConversationAsync(conversation.Id);
            await _realtime.LeaveConversationAsync(conversation.Id);
            RemoveConversationLocally(conversation.Id);
            await RefreshAllAsync(null);
        }
        catch (Exception ex)
        {
            ShowComposerError(Ui.FriendlyError(ex));
        }
    }

    private void RemoveConversationLocally(int conversationId)
    {
        _conversations.RemoveAll(c => c.Id == conversationId);
        _unreadCounts.Remove(conversationId);

        if (_current?.Id == conversationId)
        {
            _current = null;
            _messages.Clear();
            _messageIds.Clear();
            _typingUsers.Clear();
            _typingExpirations.Clear();
            UpdateHeader();
            ShowEmptyState("Conversation supprimée", "Sélectionnez une autre conversation.");
        }

        UpdateUnreadIndicators();
        RebuildConversationList();
    }

    private async void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
            return;

        e.SuppressKeyPress = true;
        e.Handled = true;

        using var dialog = new MessageSearchForm(_api, _searchBox.Text.Trim());
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedConversationId is not { } conversationId)
            return;

        var conversation = _conversations.FirstOrDefault(c => c.Id == conversationId);
        if (conversation is null)
        {
            try
            {
                conversation = await _api.GetConversationAsync(conversationId);
                _conversations.Add(conversation);
            }
            catch (Exception ex)
            {
                ShowComposerError(Ui.FriendlyError(ex));
                return;
            }
        }

        await SelectConversationAsync(conversation, force: true);
    }

    private async Task SelectConversationAsync(ConversationDto conversation, bool force = false)
    {
        if (!force && _current?.Id == conversation.Id)
            return;

        var version = ++_loadVersion;

        await SetTypingAsync(false);

        if (_current is not null)
            await _realtime.LeaveConversationAsync(_current.Id);

        _typingUsers.Clear();
        _typingExpirations.Clear();
        _current = conversation;
        ClearUnread(conversation.Id);
        _messages.Clear();
        _messageIds.Clear();
        _page = 1;
        _hasMore = false;
        _composerError.Text = string.Empty;

        UpdateHeader();
        RebuildConversationList();
        await LoadMessagesAsync(version, initial: true);
        await _realtime.JoinConversationAsync(conversation.Id);
        await MarkCurrentConversationReadAsync();
    }

    private void UpdateHeader()
    {
        if (_current is null)
        {
            _headerAvatar.Visible = false;
            _membersButton.Visible = false;
            _titleLabel.Text = "Bienvenue sur OrgChat";
            _subtitleLabel.Text = "Sélectionnez une conversation pour commencer.";
            SetComposerEnabled(false);
            return;
        }

        var title = _current.DisplayTitle(_auth.UserId);
        _headerAvatar.Visible = true;
        _headerAvatar.DisplayName = title;
        _headerAvatar.Seed = _current.Id.ToString();
        _headerAvatar.ShowStatus = true;
        _headerAvatar.Online = IsConversationOnline(_current);
        _headerAvatar.Invalidate();

        _titleLabel.Text = title;
        RefreshHeaderSubtitle();
        _membersButton.Visible = _current.IsGroup;
        SetComposerEnabled(true);
    }

    private void RefreshHeaderSubtitle()
    {
        if (_current is null)
            return;

        var typing = _typingUsers
            .Where(pair => pair.Key != _auth.UserId)
            .Select(pair => ResolveTypingName(pair.Key, pair.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _subtitleLabel.Text = typing.Count switch
        {
            0 => _current.ParticipantSummary(_auth.UserId),
            1 => $"{typing[0]} est en train d’écrire…",
            2 => $"{typing[0]} et {typing[1]} écrivent…",
            _ => $"{typing.Count} personnes écrivent…"
        };
    }

    private string ResolveTypingName(int userId, string fallback)
    {
        var participant = _current?.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant is not null)
            return string.IsNullOrWhiteSpace(participant.DisplayName) ? participant.Username : participant.DisplayName;

        var user = _users.FirstOrDefault(u => u.Id == userId);
        if (user is not null)
            return string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;

        return string.IsNullOrWhiteSpace(fallback) ? "Quelqu’un" : fallback;
    }

    private bool IsConversationOnline(ConversationDto conversation) =>
        _onlineUsers.Count > 0 &&
        conversation.Participants.Any(p => p.UserId != _auth.UserId && _onlineUsers.Contains(p.UserId));

    private void SetComposerEnabled(bool enabled)
    {
        _composerBox.Enabled = enabled;
        _attachButton.Enabled = enabled && !_sending;
        _sendButton.Enabled = enabled && !_sending;
        _composerBox.PlaceholderText = enabled
            ? "Écrivez un message…  (Entrée pour envoyer)"
            : "Sélectionnez une conversation pour écrire.";

        if (!enabled)
        {
            _composerBox.Clear();
            _ = SetTypingAsync(false);
        }
    }

    // --------------------------------------------------------------- Messages

    private async Task LoadMessagesAsync(int version, bool initial)
    {
        if (_current is null)
            return;

        _messageCts?.Cancel();
        _messageCts?.Dispose();
        var cts = new CancellationTokenSource();
        _messageCts = cts;

        var conversationId = _current.Id;
        _loadOlderLink.Enabled = false;
        _loadOlderLink.Text = "Chargement…";

        try
        {
            var paged = await _api.GetMessagesAsync(conversationId, 1, _options.PageSize, cts.Token);
            if (version != _loadVersion || cts.IsCancellationRequested || _current is null)
                return;

            _messages.Clear();
            _messageIds.Clear();
            _messages.AddRange(paged.Items.OrderBy(m => m.CreatedAt));
            foreach (var message in _messages)
                _messageIds.Add(message.Id);

            _page = paged.Page;
            _hasMore = paged.HasNextPage;

            ShowConversationView();
            RebuildMessages(scrollToBottom: initial);
            UpdateOlderLink();
        }
        catch (OperationCanceledException)
        {
            // Une autre conversation a été sélectionnée entre-temps.
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
                ShowComposerError(Ui.FriendlyError(ex));
        }
        finally
        {
            if (ReferenceEquals(_messageCts, cts))
            {
                _loadOlderLink.Enabled = true;
                if (_loadOlderLink.Text == "Chargement…")
                    UpdateOlderLink();
            }
        }
    }

    private async Task LoadOlderAsync()
    {
        if (_current is null || !_hasMore || _loadingOlder)
            return;

        var version = _loadVersion;
        _loadingOlder = true;
        _loadOlderLink.Enabled = false;
        _loadOlderLink.Text = "Chargement…";

        try
        {
            var paged = await _api.GetMessagesAsync(_current.Id, _page + 1, _options.PageSize);
            if (version != _loadVersion || _current is null)
                return;

            var older = paged.Items
                .OrderBy(m => m.CreatedAt)
                .Where(m => _messageIds.Add(m.Id))
                .ToList();

            if (older.Count > 0)
            {
                _messages.InsertRange(0, older);
                RebuildMessages(scrollToBottom: false);
            }

            _page = paged.Page;
            _hasMore = paged.HasNextPage;
            UpdateOlderLink();
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
                ShowComposerError(Ui.FriendlyError(ex));
        }
        finally
        {
            _loadingOlder = false;
            _loadOlderLink.Enabled = true;
            if (_loadOlderLink.Text == "Chargement…")
                UpdateOlderLink();
        }
    }

    private void RebuildMessages(bool scrollToBottom)
    {
        _messagesPanel.SuspendLayout();
        _messagesPanel.Controls.Clear();

        var showSender = _current?.IsGroup == true;
        var width = MessageWidth();

        foreach (var message in _messages)
        {
            var bubble = new MessageBubble(message, message.SenderId == _auth.UserId,
                showSender && message.SenderId != _auth.UserId);
            bubble.AttachmentRequested += OnAttachmentRequested;
            bubble.EditRequested += OnEditMessageRequested;
            bubble.DeleteRequested += OnDeleteMessageRequested;
            bubble.Fit(width);
            _messagesPanel.Controls.Add(bubble);
        }

        _messagesPanel.ResumeLayout();
        UpdateReadReceipts();

        BeginInvoke(new Action(() =>
        {
            if (scrollToBottom)
                ScrollToBottom();
            else
                _messagesPanel.AutoScrollPosition = new Point(0, 0);
        }));
    }

    private void AppendMessage(MessageDto message)
    {
        if (!_messageIds.Add(message.Id))
            return;

        _messages.Add(message);

        var bubble = new MessageBubble(
            message,
            message.SenderId == _auth.UserId,
            _current?.IsGroup == true && message.SenderId != _auth.UserId);
        bubble.AttachmentRequested += OnAttachmentRequested;
        bubble.EditRequested += OnEditMessageRequested;
        bubble.DeleteRequested += OnDeleteMessageRequested;
        bubble.Fit(MessageWidth());
        _messagesPanel.Controls.Add(bubble);
        UpdateReadReceipts();
        BeginInvoke(new Action(ScrollToBottom));
    }

    private int MessageWidth() =>
        Math.Max(320, _messagesPanel.ClientSize.Width - _messagesPanel.Padding.Horizontal - 24);

    private void ResizeMessageBubbles()
    {
        var width = MessageWidth();
        foreach (Control control in _messagesPanel.Controls)
        {
            if (control is MessageBubble bubble)
                bubble.Fit(width);
        }
    }

    private void ScrollToBottom()
    {
        if (_messagesPanel.Controls.Count == 0)
            return;

        _messagesPanel.ScrollControlIntoView(_messagesPanel.Controls[_messagesPanel.Controls.Count - 1]);
    }

    private void UpdateOlderLink()
    {
        _loadOlderLink.Visible = _hasMore;
        _loadOlderLink.Text = "Charger les messages plus anciens";
    }

    private void ShowConversationView()
    {
        _messagesPanel.Visible = true;
        _emptyState.Visible = false;
    }

    private void ShowEmptyState(string title, string subtitle)
    {
        _emptyTitle.Text = title;
        _emptySubtitle.Text = subtitle;
        _emptyState.Visible = true;
        _messagesPanel.Visible = false;
        _loadOlderLink.Visible = false;
    }

    // --------------------------------------------------------------- Sending

    private async void OnComposerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter || e.Shift)
            return;

        e.SuppressKeyPress = true;
        e.Handled = true;
        await SendMessageAsync();
    }

    private async void OnComposerTextChanged(object? sender, EventArgs e)
    {
        if (!_composerBox.Enabled || _current is null)
            return;

        await SetTypingAsync(!string.IsNullOrWhiteSpace(_composerBox.Text));
    }

    private async Task SetTypingAsync(bool isTyping)
    {
        if (!isTyping)
        {
            _typingTimer.Stop();
            if (!_typingSent || _typingConversationId is not { } conversationId)
                return;

            _typingSent = false;
            _typingConversationId = null;
            await _realtime.SetTypingAsync(conversationId, false);
            return;
        }

        if (_current is null || !_composerBox.Enabled || _sending)
            return;

        _typingTimer.Stop();
        _typingTimer.Start();

        if (_typingSent)
            return;

        _typingSent = true;
        _typingConversationId = _current.Id;
        await _realtime.SetTypingAsync(_current.Id, true);
    }

    private async Task SendMessageAsync()
    {
        if (_current is null || _sending)
            return;

        var text = _composerBox.Text.Trim();
        if (text.Length == 0)
            return;

        var conversation = _current;
        _sending = true;
        _sendButton.Enabled = false;
        _attachButton.Enabled = false;
        _composerError.Text = string.Empty;
        _composerBox.Clear();
        await SetTypingAsync(false);

        try
        {
            if (_realtime.IsConnected)
            {
                await _realtime.SendMessageAsync(conversation.Id, text);
            }
            else
            {
                var message = await _api.SendMessageAsync(conversation.Id, text);
                if (_current?.Id == conversation.Id)
                    AppendMessage(message);
                UpdateConversationPreview(message);
                RebuildConversationList();
            }
        }
        catch (Exception ex)
        {
            _composerBox.Text = text;
            _composerBox.SelectionStart = _composerBox.TextLength;
            ShowComposerError(Ui.FriendlyError(ex));
        }
        finally
        {
            _sending = false;
            _sendButton.Enabled = _current is not null;
            _attachButton.Enabled = _current is not null;
            _composerBox.Focus();
        }
    }

    private async void OnAttachClicked(object? sender, EventArgs e)
    {
        if (_current is null || _sending)
            return;

        using var dialog = new OpenFileDialog
        {
            Title = "Joindre un fichier",
            Filter = "Tous les fichiers (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var info = new FileInfo(dialog.FileName);
        if (info.Length <= 0)
        {
            ShowComposerError("Le fichier sélectionné est vide.");
            return;
        }

        if (info.Length > 20 * 1024 * 1024)
        {
            ShowComposerError("La pièce jointe dépasse la taille maximale de 20 Mo.");
            return;
        }

        var conversation = _current;
        var caption = _composerBox.Text.Trim();

        _sending = true;
        _sendButton.Enabled = false;
        _attachButton.Enabled = false;
        UseWaitCursor = true;
        _composerError.Text = string.Empty;
        _composerBox.Clear();
        await SetTypingAsync(false);

        try
        {
            var message = await _api.UploadAttachmentAsync(
                conversation.Id,
                dialog.FileName,
                caption.Length > 0 ? caption : null);

            if (_current?.Id == conversation.Id)
                AppendMessage(message);

            UpdateConversationPreview(message);
            RebuildConversationList();
        }
        catch (Exception ex)
        {
            if (caption.Length > 0)
                _composerBox.Text = caption;
            ShowComposerError(Ui.FriendlyError(ex));
        }
        finally
        {
            _sending = false;
            UseWaitCursor = false;
            _sendButton.Enabled = _current is not null;
            _attachButton.Enabled = _current is not null;
            _composerBox.Focus();
        }
    }

    private async void OnAttachmentRequested(AttachmentDto attachment)
    {
        try
        {
            UseWaitCursor = true;
            var bytes = await _api.DownloadAttachmentAsync(attachment.Id);
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OrgChat",
                "attachments");
            Directory.CreateDirectory(directory);

            var fileName = Path.GetFileName(attachment.OriginalFileName);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "piece-jointe";

            var path = Path.Combine(directory, $"{attachment.Id}-{fileName}");
            await File.WriteAllBytesAsync(path, bytes);

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                Ui.FriendlyError(ex),
                "Pièce jointe",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async void OnEditMessageRequested(MessageDto message)
    {
        if (_current is null || message.SenderId != _auth.UserId)
            return;

        using var dialog = new MessageEditForm(_api, message);
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.UpdatedMessage is not null)
            ApplyMessageUpdated(dialog.UpdatedMessage);
    }

    private async void OnDeleteMessageRequested(MessageDto message)
    {
        if (_current is null || message.SenderId != _auth.UserId)
            return;

        var confirm = MessageBox.Show(this,
            "Supprimer ce message ? Cette action est définitive.",
            "Supprimer le message",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
            return;

        try
        {
            var deleted = await _api.DeleteMessageAsync(message.ConversationId, message.Id);
            ApplyMessageDeleted(deleted);
        }
        catch (Exception ex)
        {
            ShowComposerError(Ui.FriendlyError(ex));
        }
    }

    private void ApplyMessageUpdated(MessageDto message)
    {
        var index = _messages.FindIndex(m => m.Id == message.Id);
        if (index >= 0)
            _messages[index] = message;

        if (_current?.Id == message.ConversationId && _messagesPanel.Visible)
            RebuildMessages(scrollToBottom: false);

        var conversation = _conversations.FirstOrDefault(c => c.Id == message.ConversationId);
        if (conversation is not null && conversation.LastMessageAt is { } lastAt &&
            Math.Abs((lastAt - message.CreatedAt).TotalSeconds) < 1)
        {
            conversation.LastMessagePreview = message.Preview;
            RebuildConversationList();
        }
    }

    private void ApplyMessageDeleted(MessageDeletedDto deleted)
    {
        var index = _messages.FindIndex(m => m.Id == deleted.Id);
        if (index >= 0)
            _messages.RemoveAt(index);
        _messageIds.Remove(deleted.Id);

        if (_current?.Id == deleted.ConversationId)
            RebuildMessages(scrollToBottom: false);

        _ = RefreshConversationSummariesAsync();
    }

    private async Task RefreshConversationSummariesAsync()
    {
        try
        {
            var conversations = await _api.GetConversationsAsync();
            _conversations = conversations;
            foreach (var conversation in _conversations)
                _unreadCounts[conversation.Id] = conversation.UnreadCount;

            var currentId = _current?.Id;
            if (currentId is not null)
                _current = _conversations.FirstOrDefault(c => c.Id == currentId) ?? _current;

            UpdateUnreadIndicators();
            RebuildConversationList();
            if (_current is not null)
                UpdateHeader();
        }
        catch
        {
            // Le résumé sera rafraîchi à la prochaine actualisation.
        }
    }

    // -------------------------------------------------------------- Realtime

    private void OnRealtimeMessage(MessageDto message)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action<MessageDto>(OnRealtimeMessage), message);
            return;
        }

        UpdateConversationPreview(message);

        var isOwnMessage = message.SenderId == _auth.UserId;
        var isCurrentConversation = _current?.Id == message.ConversationId;
        var isVisibleNow = isCurrentConversation && IsWindowActive();

        if (isCurrentConversation)
        {
            AppendMessage(message);
            if (_emptyState.Visible)
                ShowConversationView();
            if (isVisibleNow)
                ScheduleMarkRead();
        }

        if (!isOwnMessage && !isVisibleNow)
        {
            _unreadCounts[message.ConversationId] = _unreadCounts.GetValueOrDefault(message.ConversationId) + 1;
            var conversation = _conversations.FirstOrDefault(c => c.Id == message.ConversationId);
            if (conversation is not null)
                conversation.UnreadCount = _unreadCounts[message.ConversationId];
            UpdateUnreadIndicators();

            if ((!IsWindowActive() || !isCurrentConversation) && _toastedMessageIds.Add(message.Id))
                ShowMessageNotification(message);
        }

        RebuildConversationList();
    }

    private void OnRealtimeMessageUpdated(MessageDto message)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action<MessageDto>(OnRealtimeMessageUpdated), message);
            return;
        }

        ApplyMessageUpdated(message);
    }

    private void OnRealtimeMessageDeleted(MessageDeletedDto deleted)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action<MessageDeletedDto>(OnRealtimeMessageDeleted), deleted);
            return;
        }

        ApplyMessageDeleted(deleted);
    }

    private void OnRealtimeConversationDeleted(ConversationDeletedDto deleted)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action<ConversationDeletedDto>(OnRealtimeConversationDeleted), deleted);
            return;
        }

        RemoveConversationLocally(deleted.Id);
    }

    private void OnRealtimeConversationUpdated(ConversationDto conversation)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action<ConversationDto>(OnRealtimeConversationUpdated), conversation);
            return;
        }

        var index = _conversations.FindIndex(c => c.Id == conversation.Id);
        if (index >= 0)
            _conversations[index] = conversation;
        else
            _conversations.Add(conversation);

        if (_current?.Id == conversation.Id)
        {
            _current = conversation;
            UpdateHeader();
        }

        RebuildConversationList();
    }

    private void OnPresenceChanged(PresenceChangedDto presence)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action<PresenceChangedDto>(OnPresenceChanged), presence);
            return;
        }

        if (presence.UserId == _auth.UserId)
            return;

        if (presence.IsOnline)
            _onlineUsers.Add(presence.UserId);
        else
            _onlineUsers.Remove(presence.UserId);

        RebuildConversationList();
        if (_current is not null && _current.Participants.Any(p => p.UserId == presence.UserId))
        {
            _headerAvatar.Online = IsConversationOnline(_current);
            _headerAvatar.Invalidate();
        }
    }

    private void OnUserTyping(UserTypingDto typing)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action<UserTypingDto>(OnUserTyping), typing);
            return;
        }

        if (typing.UserId == _auth.UserId || _current?.Id != typing.ConversationId)
            return;

        if (typing.IsTyping)
        {
            _typingUsers[typing.UserId] = typing.Username;
            _typingExpirations[typing.UserId] = DateTime.UtcNow.AddSeconds(4.5);
        }
        else
        {
            _typingUsers.Remove(typing.UserId);
            _typingExpirations.Remove(typing.UserId);
        }

        RefreshHeaderSubtitle();
    }

    private void ExpireTypingUsers()
    {
        if (_typingExpirations.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var expired = _typingExpirations
            .Where(pair => pair.Value <= now)
            .Select(pair => pair.Key)
            .ToList();

        if (expired.Count == 0)
            return;

        foreach (var userId in expired)
        {
            _typingExpirations.Remove(userId);
            _typingUsers.Remove(userId);
        }

        RefreshHeaderSubtitle();
    }

    private async Task RefreshPresenceAsync()
    {
        if (!_realtime.IsConnected)
            return;

        var online = await _realtime.GetOnlineUsersAsync();
        _onlineUsers.Clear();
        foreach (var userId in online)
        {
            if (userId != _auth.UserId)
                _onlineUsers.Add(userId);
        }

        RebuildConversationList();
        if (_current is not null)
        {
            _headerAvatar.Online = IsConversationOnline(_current);
            _headerAvatar.Invalidate();
        }
    }

    private async void OnNotificationReceived(NotificationDto notification)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action<NotificationDto>(OnNotificationReceived), notification);
            return;
        }

        if (!notification.IsRead)
        {
            _notificationUnreadCount++;
            UpdateNotificationBadge();
        }

        if (notification.ConversationId == _current?.Id && IsWindowActive())
        {
            ScheduleMarkRead();
            return;
        }

        if (notification.MessageId > 0 && !_toastedMessageIds.Add(notification.MessageId))
            return;

        ShowNotificationToast(notification);
    }

    private async void OnMarkReadTimerTick(object? sender, EventArgs e)
    {
        _markReadTimer.Stop();
        await MarkCurrentConversationReadAsync();
    }

    private void ScheduleMarkRead()
    {
        if (_current is null)
            return;

        _markReadTimer.Stop();
        _markReadTimer.Start();
    }

    private async Task MarkCurrentConversationReadAsync()
    {
        var conversation = _current;
        if (conversation is null || _markingRead)
            return;

        var me = conversation.Participants.FirstOrDefault(p => p.UserId == _auth.UserId);
        var lastReadAt = me?.LastReadAt is { } storedReadAt ? ToUtc(storedReadAt) : (DateTime?)null;
        var hasUnread = conversation.UnreadCount > 0 ||
            _messages.Any(m => !m.IsDeleted &&
                               m.SenderId != _auth.UserId &&
                               (lastReadAt is null || ToUtc(m.CreatedAt) > lastReadAt.Value));

        if (!hasUnread)
        {
            ClearUnread(conversation.Id);
            return;
        }

        _markingRead = true;
        try
        {
            ReadReceiptDto? receipt = null;
            if (_realtime.IsConnected)
                receipt = await _realtime.MarkAsReadAsync(conversation.Id);

            receipt ??= await _api.MarkConversationReadAsync(conversation.Id);
            ApplyReadReceipt(receipt);
            await RefreshNotificationCountAsync();
        }
        catch
        {
            // La lecture sera retentée à la prochaine activation de la conversation.
        }
        finally
        {
            _markingRead = false;
        }
    }

    private void OnConversationRead(ReadReceiptDto receipt)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action<ReadReceiptDto>(OnConversationRead), receipt);
            return;
        }

        ApplyReadReceipt(receipt);
    }

    private void ApplyReadReceipt(ReadReceiptDto receipt)
    {
        var conversation = _conversations.FirstOrDefault(c => c.Id == receipt.ConversationId);
        if (conversation is null)
            return;

        var participant = conversation.Participants.FirstOrDefault(p => p.UserId == receipt.UserId);
        if (participant is not null)
            participant.LastReadAt = receipt.ReadAt;

        if (receipt.UserId == _auth.UserId)
        {
            conversation.UnreadCount = 0;
            ClearUnread(conversation.Id);
        }

        if (_current?.Id == receipt.ConversationId)
            UpdateReadReceipts();
    }

    private void UpdateReadReceipts()
    {
        if (_current is null || _messagesPanel.IsDisposed)
            return;

        var others = _current.Participants
            .Where(p => p.UserId != _auth.UserId)
            .ToList();
        var lastOwn = _messages.LastOrDefault(m => m.SenderId == _auth.UserId && !m.IsDeleted);

        string? receipt = null;
        if (lastOwn is not null && others.Count > 0)
        {
            var lastOwnAt = ToUtc(lastOwn.CreatedAt);
            var readCount = others.Count(p =>
                p.LastReadAt is { } readAt && ToUtc(readAt) >= lastOwnAt);

            if (readCount > 0)
                receipt = _current.IsGroup ? $"Vu par {readCount}/{others.Count}" : "Vu";
        }

        var width = MessageWidth();
        foreach (Control control in _messagesPanel.Controls)
        {
            if (control is not MessageBubble bubble)
                continue;

            var text = lastOwn is not null && ReferenceEquals(bubble.Message, lastOwn)
                ? receipt
                : null;

            if (string.Equals(bubble.ReceiptText, text, StringComparison.Ordinal))
                continue;

            bubble.ReceiptText = text;
            bubble.Fit(width);
        }
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private bool IsWindowActive() =>
        !IsDisposed
        && WindowState != FormWindowState.Minimized
        && Form.ActiveForm is not null;

    private void ClearUnread(int conversationId)
    {
        if (!_unreadCounts.Remove(conversationId))
            return;

        UpdateUnreadIndicators();
        RebuildConversationList();
    }

    private void UpdateUnreadIndicators()
    {
        var total = _unreadCounts.Values.Sum();
        Text = total > 0 ? $"OrgChat ({total})" : "OrgChat";

        if (_notifyIcon is not null)
            _notifyIcon.Text = total > 0 ? $"OrgChat — {total} message(s) non lu(s)" : "OrgChat";
    }

    private void ShowMessageNotification(MessageDto message)
    {
        if (_notifyIcon is null || !_notifyIcon.Visible)
            return;

        var conversation = _conversations.FirstOrDefault(c => c.Id == message.ConversationId);
        var title = conversation?.DisplayTitle(_auth.UserId) ?? "Nouveau message";
        var sender = ResolveSenderName(message, conversation);
        var body = $"{sender} : {message.Preview}";

        ShowNotificationBalloon(message.ConversationId, title, body);
    }

    private void ShowNotificationToast(NotificationDto notification)
    {
        if (notification.ConversationId <= 0)
            return;

        var title = string.IsNullOrWhiteSpace(notification.ConversationTitle)
            ? "Nouveau message"
            : notification.ConversationTitle;
        var body = string.IsNullOrWhiteSpace(notification.SenderName)
            ? notification.Preview
            : $"{notification.SenderName} : {notification.Preview}";

        ShowNotificationBalloon(notification.ConversationId, title, body);
    }

    private void ShowNotificationBalloon(int conversationId, string title, string body)
    {
        if (_notifyIcon is null || !_notifyIcon.Visible)
            return;

        if (title.Length > 60)
            title = title[..57] + "…";
        if (body.Length > 240)
            body = body[..237] + "…";

        _pendingNotificationConversationId = conversationId;
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = body;
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(6000);
    }

    private async Task RefreshNotificationCountAsync()
    {
        try
        {
            var count = await _api.GetNotificationUnreadCountAsync();
            _notificationUnreadCount = Math.Max(0, count.UnreadCount);
            UpdateNotificationBadge();
        }
        catch
        {
            // Le badge sera rafraîchi à la prochaine occasion.
        }
    }

    private void UpdateNotificationBadge()
    {
        if (_notificationsButton is null)
            return;

        _notificationsButton.Text = _notificationUnreadCount > 0
            ? $"🔔 {_notificationUnreadCount}"
            : "🔔";
        _notificationsButton.Width = _notificationUnreadCount > 0 ? 70 : 54;
    }

    private async void OnNotificationsClicked(object? sender, EventArgs e)
    {
        using var dialog = new NotificationsForm(_api);
        dialog.ShowDialog(this);

        await RefreshNotificationCountAsync();

        var conversationId = dialog.SelectedConversationId;
        if (conversationId is null)
            return;

        var conversation = _conversations.FirstOrDefault(c => c.Id == conversationId.Value);
        if (conversation is not null)
        {
            await SelectConversationAsync(conversation);
            return;
        }

        await RefreshAllAsync(conversationId);
    }

    private string ResolveSenderName(MessageDto message, ConversationDto? conversation)
    {
        var participant = conversation?.Participants.FirstOrDefault(p => p.UserId == message.SenderId);
        if (participant is not null)
            return string.IsNullOrWhiteSpace(participant.DisplayName) ? participant.Username : participant.DisplayName;

        var user = _users.FirstOrDefault(u => u.Id == message.SenderId);
        if (user is not null)
            return string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;

        return string.IsNullOrWhiteSpace(message.SenderUsername) ? "Nouveau message" : message.SenderUsername;
    }

    private async void RestoreFromNotification()
    {
        if (IsDisposed)
            return;

        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;

        Show();
        Activate();
        BringToFront();

        var conversationId = _pendingNotificationConversationId;
        _pendingNotificationConversationId = null;

        if (conversationId is null)
            return;

        var conversation = _conversations.FirstOrDefault(c => c.Id == conversationId.Value);
        if (conversation is not null)
            await SelectConversationAsync(conversation);
        else
            await RefreshAllAsync(conversationId);
    }

    private void UpdateConversationPreview(MessageDto message)
    {
        var conversation = _conversations.FirstOrDefault(c => c.Id == message.ConversationId);
        if (conversation is null)
            return;

        conversation.LastMessageAt = message.CreatedAt;
        conversation.LastMessagePreview = message.Preview;
    }

    private void OnRealtimeStateChanged(RealtimeState state, string? detail)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action<RealtimeState, string?>(OnRealtimeStateChanged), state, detail);
            return;
        }

        SetConnectionState(state);
    }

    private void SetConnectionState(RealtimeState state)
    {
        (_statusLabel.Text, _statusLabel.ForeColor) = state switch
        {
            RealtimeState.Connected => ("● En ligne", Palette.Success),
            RealtimeState.Connecting => ("● Connexion…", Palette.Warning),
            RealtimeState.Reconnecting => ("● Reconnexion…", Palette.Warning),
            _ => ("● Hors ligne", Palette.Danger)
        };

        if (state is RealtimeState.Disconnected or RealtimeState.Reconnecting)
        {
            _typingSent = false;
            _typingConversationId = null;
            _typingUsers.Clear();
            _typingExpirations.Clear();
            RefreshHeaderSubtitle();
        }

        if (state == RealtimeState.Disconnected)
        {
            _onlineUsers.Clear();
            RebuildConversationList();
            if (_current is not null)
            {
                _headerAvatar.Online = false;
                _headerAvatar.Invalidate();
            }
        }
    }

    private Task OnRealtimeReconnected()
    {
        if (!IsDisposed)
        {
            BeginInvoke(new Action(async () =>
            {
                _typingSent = false;
                _typingConversationId = null;
                await RefreshPresenceAsync();
                await RefreshNotificationCountAsync();

                if (_current is null)
                    return;

                await _realtime.JoinConversationAsync(_current.Id);
                await LoadMessagesAsync(++_loadVersion, initial: true);
                await MarkCurrentConversationReadAsync();
            }));
        }

        return Task.CompletedTask;
    }

    // --------------------------------------------------------------- Actions

    private async void OnNewConversationClicked(object? sender, EventArgs e)
    {
        using var dialog = new NewConversationForm(_api, _users, _auth.UserId);

        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.CreatedConversation is not null)
            await RefreshAllAsync(dialog.CreatedConversation.Id);
    }

    private async void OnMembersClicked(object? sender, EventArgs e)
    {
        if (_current is null)
            return;

        using var dialog = new GroupMembersForm(_api, _current, _auth.UserId, _users);
        dialog.ShowDialog(this);

        _current = dialog.Conversation;
        UpdateHeader();
        await RefreshAllAsync(_current.Id);
    }

    private void OnProfileClicked(object? sender, EventArgs e)
    {
        var me = _users.FirstOrDefault(u => u.Id == _auth.UserId) ?? new UserDto
        {
            Id = _auth.UserId,
            Username = _auth.Username,
            DisplayName = _auth.DisplayName,
            Email = _auth.Email,
            Role = _auth.Role,
            IsActive = true
        };

        using var dialog = new ProfileForm(_api, me);
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.UpdatedUser is not null)
        {
            var updated = dialog.UpdatedUser;
            _auth.DisplayName = updated.DisplayName;
            _auth.Email = updated.Email;
            _auth.Role = updated.Role;

            var index = _users.FindIndex(u => u.Id == updated.Id);
            if (index >= 0)
                _users[index] = updated;

            UpdateUserCard();

            var current = _current;
            if (current is not null)
            {
                foreach (var participant in current.Participants.Where(p => p.UserId == updated.Id))
                {
                    participant.DisplayName = updated.DisplayName;
                    participant.Username = updated.Username;
                }
            }

            RebuildConversationList();
            UpdateHeader();
        }
    }

    private async void OnDirectoryClicked(object? sender, EventArgs e)
    {
        using var dialog = new UsersForm(_api, _users, _auth.UserId, _auth.IsAdmin);
        dialog.ShowDialog(this);

        try
        {
            _users = await _api.GetUsersAsync();
        }
        catch
        {
            // L'annuaire reste consultable avec les données déjà chargées.
        }
    }

    private void OnLogoutClicked(object? sender, EventArgs e)
    {
        var confirm = MessageBox.Show(this,
            "Voulez-vous vous déconnecter d'OrgChat ?",
            "Déconnexion",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
            LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ShowComposerError(string message)
    {
        _composerError.Text = message;
    }

    /// <summary>Ferme réellement la fenêtre lors d'une déconnexion.</summary>
    public void CloseForLogout()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose &&
            e.CloseReason == CloseReason.UserClosing &&
            _notifyIcon is not null)
        {
            e.Cancel = true;
            Hide();
            _notifyIcon.Visible = true;
            _notifyIcon.BalloonTipTitle = "OrgChat continue en arrière-plan";
            _notifyIcon.BalloonTipText = "Double-cliquez sur l'icône pour rouvrir la fenêtre.";
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(4000);
            return;
        }

        base.OnFormClosing(e);
    }

    // --------------------------------------------------------------- Cleanup

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _messageCts?.Cancel();
        _messageCts?.Dispose();
        _realtime.MessageReceived -= OnRealtimeMessage;
        _realtime.MessageUpdated -= OnRealtimeMessageUpdated;
        _realtime.MessageDeleted -= OnRealtimeMessageDeleted;
        _realtime.ConversationDeleted -= OnRealtimeConversationDeleted;
        _realtime.ConversationUpdated -= OnRealtimeConversationUpdated;
        _realtime.ConversationRead -= OnConversationRead;
        _realtime.PresenceChanged -= OnPresenceChanged;
        _realtime.UserTyping -= OnUserTyping;
        _realtime.NotificationReceived -= OnNotificationReceived;
        _realtime.StateChanged -= OnRealtimeStateChanged;
        _realtime.Reconnected -= OnRealtimeReconnected;
        _markReadTimer.Stop();
        _markReadTimer.Dispose();
        _typingTimer.Stop();
        _typingTimer.Dispose();
        _typingUiTimer.Stop();
        _typingUiTimer.Dispose();
        _ = _realtime.DisposeAsync();

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
        }

        base.OnFormClosed(e);
    }
}
