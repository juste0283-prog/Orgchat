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
    private readonly HashSet<int> _unread = new();
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
    private RoundedButton _membersButton = null!;
    private RoundedButton _refreshButton = null!;
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
        WireRealtime();

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
        var searchField = Ui.Field("Rechercher une conversation…", out _searchBox, height: 38);
        searchField.Dock = DockStyle.Fill;
        _searchBox.TextChanged += (_, _) => RebuildConversationList();
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
            Width = 290,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 17, 20, 0)
        };

        _refreshButton = Ui.Ghost("⟳", 38, 38);
        _refreshButton.Margin = new Padding(8, 0, 0, 0);
        _refreshButton.Click += async (_, _) => await RefreshAllAsync(_current?.Id);
        actions.Controls.Add(_refreshButton);

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
            var width = Math.Max(120, _headerPanel.ClientSize.Width - 76 - 310);
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
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Palette.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

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
        grid.Controls.Add(_composerBox, 0, 0);

        _sendButton = Ui.Primary("➤", 46, 44);
        _sendButton.Dock = DockStyle.Fill;
        _sendButton.Margin = new Padding(4, 2, 0, 2);
        _sendButton.Enabled = false;
        _sendButton.Click += async (_, _) => await SendMessageAsync();
        grid.Controls.Add(_sendButton, 1, 0);

        shell.Controls.Add(grid);
        host.Controls.Add(shell);
        return host;
    }

    private void WireRealtime()
    {
        _realtime.MessageReceived += OnRealtimeMessage;
        _realtime.StateChanged += OnRealtimeStateChanged;
        _realtime.Reconnected += OnRealtimeReconnected;
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
                Unread = _unread.Contains(conversation.Id)
            };
            item.Activate += async (_, _) => await SelectConversationAsync(conversation);
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

    private async Task SelectConversationAsync(ConversationDto conversation, bool force = false)
    {
        if (!force && _current?.Id == conversation.Id)
            return;

        var version = ++_loadVersion;

        if (_current is not null)
            await _realtime.LeaveConversationAsync(_current.Id);

        _current = conversation;
        _unread.Remove(conversation.Id);
        _messages.Clear();
        _messageIds.Clear();
        _page = 1;
        _hasMore = false;
        _composerError.Text = string.Empty;

        UpdateHeader();
        RebuildConversationList();
        await LoadMessagesAsync(version, initial: true);
        await _realtime.JoinConversationAsync(conversation.Id);
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
        _headerAvatar.Invalidate();

        _titleLabel.Text = title;
        _subtitleLabel.Text = _current.ParticipantSummary(_auth.UserId);
        _membersButton.Visible = _current.IsGroup;
        SetComposerEnabled(true);
    }

    private void SetComposerEnabled(bool enabled)
    {
        _composerBox.Enabled = enabled;
        _sendButton.Enabled = enabled && !_sending;
        _composerBox.PlaceholderText = enabled
            ? "Écrivez un message…  (Entrée pour envoyer)"
            : "Sélectionnez une conversation pour écrire.";

        if (!enabled)
            _composerBox.Clear();
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
            bubble.Fit(width);
            _messagesPanel.Controls.Add(bubble);
        }

        _messagesPanel.ResumeLayout();

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
        bubble.Fit(MessageWidth());
        _messagesPanel.Controls.Add(bubble);
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

    private async Task SendMessageAsync()
    {
        if (_current is null || _sending)
            return;

        var text = _composerBox.Text.Trim();
        if (text.Length == 0)
            return;

        _sending = true;
        _sendButton.Enabled = false;
        _composerError.Text = string.Empty;
        _composerBox.Clear();

        try
        {
            if (_realtime.IsConnected)
            {
                await _realtime.SendMessageAsync(_current.Id, text);
            }
            else
            {
                var message = await _api.SendMessageAsync(_current.Id, text);
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
            _composerBox.Focus();
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

        if (_current?.Id == message.ConversationId)
        {
            AppendMessage(message);
            if (_emptyState.Visible)
                ShowConversationView();
        }
        else
        {
            _unread.Add(message.ConversationId);
        }

        RebuildConversationList();
    }

    private void UpdateConversationPreview(MessageDto message)
    {
        var conversation = _conversations.FirstOrDefault(c => c.Id == message.ConversationId);
        if (conversation is null)
            return;

        conversation.LastMessageAt = message.CreatedAt;
        conversation.LastMessagePreview = message.Content;
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
    }

    private Task OnRealtimeReconnected()
    {
        if (!IsDisposed)
        {
            BeginInvoke(new Action(async () =>
            {
                if (_current is null)
                    return;

                await _realtime.JoinConversationAsync(_current.Id);
                await LoadMessagesAsync(++_loadVersion, initial: true);
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

    // --------------------------------------------------------------- Cleanup

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _messageCts?.Cancel();
        _messageCts?.Dispose();
        _realtime.MessageReceived -= OnRealtimeMessage;
        _realtime.StateChanged -= OnRealtimeStateChanged;
        _realtime.Reconnected -= OnRealtimeReconnected;
        _ = _realtime.DisposeAsync();
        base.OnFormClosed(e);
    }
}
