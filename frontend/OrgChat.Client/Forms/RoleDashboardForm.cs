using System.Drawing.Drawing2D;
using OrgChat.Client.Configuration;
using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>
/// Tableau de bord professionnel par rôle (SuperAdmin, OrgAdmin,
/// Superviseur, Responsable de département). Reprend la maquette OrgChat :
/// barre latérale bleu nuit, cartes d'indicateurs, courbes d'activité,
/// anneau de répartition, liste des départements et aperçu de la messagerie.
/// Les utilisateurs simples ouvrent directement la messagerie (MainForm).
/// </summary>
public sealed class RoleDashboardForm : Form
{
    private readonly ApiClient _api;
    private readonly AuthResponse _auth;

    // Topbar
    private Label _dateRangeLabel = null!;
    private RoundedButton _notificationsButton = null!;
    private Label _onlineLabel = null!;
    private Label _loadingLabel = null!;
    private Label _errorLabel = null!;

    // KPI
    private Label _usersValue = null!;
    private Label _usersSub = null!;
    private Label _activeValue = null!;
    private Label _activeSub = null!;
    private Label _messagesValue = null!;
    private Label _messagesSub = null!;
    private Label _onlineValue = null!;
    private Label _onlineSub = null!;

    // Graphiques
    private LineChartPanel _activityChart = null!;
    private Label _activityPeriodLabel = null!;
    private DonutChartPanel _departmentDonut = null!;

    // Métriques secondaires
    private Label _groupsValue = null!;
    private Label _messages24Value = null!;
    private Label _incidentsValue = null!;
    private Label _departmentsValue = null!;

    // Départements
    private ThemedListView _departmentsView = null!;
    private Panel _departmentsEmpty = null!;

    // Messagerie
    private SmoothFlowPanel _conversationList = null!;
    private TextBox _conversationSearch = null!;

    // Supervision
    private Label _healthPillLabel = null!;
    private RoundedPanel _healthPill = null!;
    private Label _connValue = null!;
    private Label _convValue = null!;
    private Label _incidentsSupervisionValue = null!;

    // Données
    private List<UserDto> _users = new();
    private List<DepartmentDto> _departments = new();
    private List<ConversationDto> _conversations = new();
    private SupervisionSummaryDto? _summary;
    private List<ActivityPointDto> _activity = new();
    private SupervisionHealthDto? _health;
    private int _incidentCount;
    private int _notificationUnreadCount;
    private bool _busy;

    public event EventHandler? OpenMessagingRequested;
    public event EventHandler? LogoutRequested;
    public event EventHandler? SettingsChanged;

    public RoleDashboardForm(ApiClient api, ClientOptions options, AuthResponse auth)
    {
        _api = api;
        _auth = auth;

        Ui.StyleForm(this);
        Text = "OrgChat · Tableau de bord";
        ClientSize = new Size(1320, 900);
        MinimumSize = new Size(1180, 760);
        DoubleBuffered = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            BackColor = Palette.Background,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(BuildMain(), 1, 0);
        Controls.Add(root);

        Localization.Apply(this);
        _ = LoadAllAsync();
    }

    private bool CanAdmin => _auth.Role is "SuperAdmin" or "OrgAdmin";

    private bool CanSupervise => _auth.Role is "SuperAdmin" or "OrgAdmin" or "Supervisor";

    // --------------------------------------------------------------- Sidebar

    private Control BuildSidebar()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Palette.DashboardSidebar,
            Padding = Padding.Empty,
        };

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(14, 20, 14, 14),
            BackColor = Color.Transparent,
        };
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

        stack.Controls.Add(BuildBrand(), 0, 0);
        stack.Controls.Add(BuildNavigation(), 0, 1);
        stack.Controls.Add(BuildUserCard(), 0, 2);

        var version = Ui.Label("OrgChat · v1.0", Palette.Small, Palette.DashboardSidebarMuted, autoSize: false);
        version.Dock = DockStyle.Fill;
        version.TextAlign = ContentAlignment.BottomLeft;
        stack.Controls.Add(version, 0, 3);

        sidebar.Controls.Add(stack);
        return sidebar;
    }

    private static Control BuildBrand()
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var mark = new BrandMark { Location = new Point(2, 2), Size = new Size(42, 42) };
        host.Controls.Add(mark);

        var brand = Ui.Label("OrgChat", new Font("Segoe UI", 17f, FontStyle.Bold),
            Palette.DashboardSidebarText, autoSize: false);
        brand.Location = new Point(54, 2);
        brand.Size = new Size(170, 30);
        host.Controls.Add(brand);

        var subtitle = Ui.Label("Espace de travail", Palette.Small, Palette.DashboardSidebarMuted, autoSize: false);
        subtitle.Location = new Point(55, 33);
        subtitle.Size = new Size(170, 18);
        host.Controls.Add(subtitle);

        return host;
    }

    private Control BuildNavigation()
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

        var section = Ui.Label("NAVIGATION", Palette.SmallBold, Palette.DashboardSidebarMuted, autoSize: false);
        section.Location = new Point(12, 4);
        section.Size = new Size(190, 18);
        host.Controls.Add(section);

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 28, 0, 0),
        };

        var dashboard = AddNav(nav, "▦", "Tableau de bord", active: true);
        dashboard.Click += (_, _) => { };

        var users = AddNav(nav, "◉", "Utilisateurs", enabled: CanAdmin);
        users.Click += OnManageUsersClicked;

        var departments = AddNav(nav, "▭", "Départements");
        departments.Click += OnManageDepartmentsClicked;

        var messaging = AddNav(nav, "✉", "Messagerie");
        messaging.Click += (_, _) => OpenMessagingRequested?.Invoke(this, EventArgs.Empty);

        var supervision = AddNav(nav, "↗", "Supervision", enabled: CanSupervise);
        supervision.Click += OnSupervisionClicked;

        var reports = AddNav(nav, "▤", "Rapports", enabled: CanSupervise);
        reports.Click += OnSupervisionClicked;

        var audit = AddNav(nav, "⛨", "Journal d'audit", enabled: CanSupervise);
        audit.Click += OnSupervisionClicked;

        var settings = AddNav(nav, "⚙", "Paramètres");
        settings.Click += (_, _) => SettingsChanged?.Invoke(this, EventArgs.Empty);

        nav.Resize += (_, _) =>
        {
            foreach (Control control in nav.Controls)
                control.Width = Math.Max(120, nav.ClientSize.Width - 4);
        };

        host.Controls.Add(nav);
        return host;
    }

    private static SidebarNavButton AddNav(FlowLayoutPanel nav, string glyph, string text,
        bool active = false, bool enabled = true)
    {
        var button = new SidebarNavButton(glyph, text)
        {
            Width = Math.Max(120, nav.ClientSize.Width - 4),
            Height = 42,
            Active = active,
            Enabled = enabled,
            Margin = new Padding(0, 0, 0, 4),
        };
        nav.Controls.Add(button);
        return button;
    }

    private Control BuildUserCard()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12,
            FillColor = Palette.DashboardSidebarCard,
            BorderColor = Palette.DashboardSidebarBorder,
            Padding = Padding.Empty,
        };

        var avatar = new AvatarControl
        {
            DisplayName = _auth.DisplayName,
            Seed = _auth.UserId.ToString(),
            Location = new Point(12, 14),
            Size = new Size(42, 42),
            ShowStatus = true,
            Online = true,
        };
        card.Controls.Add(avatar);

        var name = Ui.Label(_auth.DisplayName, Palette.SmallBold, Palette.DashboardSidebarText, autoSize: false);
        name.Location = new Point(64, 13);
        name.Size = new Size(125, 20);
        name.AutoEllipsis = true;
        card.Controls.Add(name);

        var role = Ui.Label(RoleShortLabel(_auth.Role), Palette.Small, Palette.DashboardSidebarMuted, autoSize: false);
        role.Location = new Point(64, 32);
        role.Size = new Size(125, 18);
        role.AutoEllipsis = true;
        card.Controls.Add(role);

        var email = Ui.Label(_auth.Email, Palette.Small, Palette.DashboardSidebarMuted, autoSize: false);
        email.Location = new Point(14, 66);
        email.Size = new Size(190, 18);
        email.AutoEllipsis = true;
        card.Controls.Add(email);

        var logout = new RoundedButton
        {
            Text = "⏻",
            Width = 32,
            Height = 32,
            CornerRadius = 8,
            Font = new Font("Segoe UI Symbol", 11f, FontStyle.Bold),
            NormalColor = Palette.DashboardSidebarCard,
            HoverColor = Palette.DashboardSidebarHover,
            PressedColor = Palette.DashboardSidebarHover,
            ForeColor = Palette.DashboardSidebarText,
            Location = new Point(192, 10),
        };
        logout.Click += (_, _) => LogoutRequested?.Invoke(this, EventArgs.Empty);
        card.Controls.Add(logout);

        void OpenProfile(object? sender, EventArgs e)
        {
            _ = OpenProfileAsync();
        }

        card.Click += OpenProfile;
        avatar.Click += OpenProfile;
        name.Click += OpenProfile;
        role.Click += OpenProfile;

        return card;
    }

    // ------------------------------------------------------------------ Main

    private Control BuildMain()
    {
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24, 16, 24, 8),
            BackColor = Palette.Background,
        };
        main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

        main.Controls.Add(BuildTopbar(), 0, 0);
        main.Controls.Add(BuildScrollableContent(), 0, 1);

        var status = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        _loadingLabel = Ui.Label(string.Empty, Palette.Small, Palette.Muted, autoSize: false);
        _loadingLabel.Dock = DockStyle.Left;
        _loadingLabel.Width = 310;
        _loadingLabel.TextAlign = ContentAlignment.MiddleLeft;
        status.Controls.Add(_loadingLabel);

        _errorLabel = Ui.Label(string.Empty, Palette.Small, Palette.Danger, autoSize: false);
        _errorLabel.Dock = DockStyle.Fill;
        _errorLabel.TextAlign = ContentAlignment.MiddleRight;
        status.Controls.Add(_errorLabel);

        main.Controls.Add(status, 0, 2);
        return main;
    }

    private Control BuildTopbar()
    {
        var topbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Palette.Background,
        };
        topbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        topbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 552));
        topbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var titleHost = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var title = Ui.Label("Tableau de bord", new Font("Segoe UI", 21f, FontStyle.Bold),
            Palette.Text, autoSize: false);
        title.Location = new Point(0, 2);
        title.Size = new Size(420, 36);
        titleHost.Controls.Add(title);

        var welcome = Ui.Label(
            $"Bonjour {_auth.DisplayName} · Voici l'activité de votre organisation.",
            Palette.Body,
            Palette.Muted,
            autoSize: false);
        welcome.Location = new Point(1, 42);
        welcome.Size = new Size(460, 22);
        welcome.AutoEllipsis = true;
        titleHost.Controls.Add(welcome);

        topbar.Controls.Add(titleHost, 0, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 8, 0, 0),
        };

        var profile = new RoundedPanel
        {
            Width = 184,
            Height = 46,
            CornerRadius = 11,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            Margin = new Padding(8, 0, 0, 0),
            Cursor = Cursors.Hand,
        };
        var profileAvatar = new AvatarControl
        {
            DisplayName = _auth.DisplayName,
            Seed = _auth.UserId.ToString(),
            Location = new Point(7, 7),
            Size = new Size(32, 32),
        };
        profile.Controls.Add(profileAvatar);
        var profileName = Ui.Label(_auth.DisplayName, Palette.Small, Palette.Text, autoSize: false);
        profileName.Location = new Point(46, 6);
        profileName.Size = new Size(130, 18);
        profileName.AutoEllipsis = true;
        profile.Controls.Add(profileName);
        var profileRole = Ui.Label(RoleShortLabel(_auth.Role), Palette.Small, Palette.Muted, autoSize: false);
        profileRole.Location = new Point(46, 24);
        profileRole.Size = new Size(130, 16);
        profileRole.AutoEllipsis = true;
        profile.Controls.Add(profileRole);
        profile.Click += (_, _) => _ = OpenProfileAsync();
        profileAvatar.Click += (_, _) => _ = OpenProfileAsync();
        actions.Controls.Add(profile);

        _notificationsButton = Ui.Ghost("🔔", 56, 42);
        _notificationsButton.Margin = new Padding(8, 2, 0, 0);
        _notificationsButton.Click += OnNotificationsClicked;
        actions.Controls.Add(_notificationsButton);

        var refresh = Ui.Ghost("⟳", 42, 42);
        refresh.Margin = new Padding(8, 2, 0, 0);
        refresh.Click += async (_, _) => await LoadAllAsync();
        actions.Controls.Add(refresh);

        var range = new RoundedPanel
        {
            Width = 236,
            Height = 42,
            CornerRadius = 10,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            Margin = new Padding(8, 2, 0, 0),
        };
        var rangeIcon = Ui.Label("▦", new Font("Segoe UI Symbol", 11f), Palette.Accent, autoSize: false);
        rangeIcon.Location = new Point(12, 10);
        rangeIcon.Size = new Size(20, 20);
        range.Controls.Add(rangeIcon);
        var rangeCaption = Ui.Label("PÉRIODE", Palette.SmallBold, Palette.Faint, autoSize: false);
        rangeCaption.Location = new Point(38, 5);
        rangeCaption.Size = new Size(180, 14);
        range.Controls.Add(rangeCaption);
        _dateRangeLabel = Ui.Label(string.Empty, Palette.SmallBold, Palette.Text, autoSize: false);
        _dateRangeLabel.Location = new Point(38, 20);
        _dateRangeLabel.Size = new Size(190, 16);
        range.Controls.Add(_dateRangeLabel);
        actions.Controls.Add(range);

        topbar.Controls.Add(actions, 1, 0);
        return topbar;
    }

    private Control BuildScrollableContent()
    {
        // Le tableau de bord occupe exactement l'espace disponible : les rangées
        // sont proportionnelles pour rester complètes sur un écran 1366x768.
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Palette.Background,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68f));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        content.Controls.Add(BuildLeftColumn(), 0, 0);
        content.Controls.Add(BuildRightColumn(), 1, 0);
        return content;
    }

    private Control BuildLeftColumn()
    {
        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            BackColor = Palette.Background,
            Margin = new Padding(0, 0, 7, 0),
        };
        left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 15f));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 33f));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 13f));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 39f));

        left.Controls.Add(BuildKpiRow(), 0, 0);
        left.Controls.Add(BuildChartsRow(), 0, 2);
        left.Controls.Add(BuildMetricsRow(), 0, 4);
        left.Controls.Add(BuildBottomRow(), 0, 6);
        return left;
    }

    private Control BuildRightColumn()
    {
        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Palette.Background,
            Margin = new Padding(7, 0, 0, 0),
        };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 64f));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 36f));

        right.Controls.Add(BuildMessagingCard(), 0, 0);
        right.Controls.Add(BuildSupervisionCard(), 0, 2);
        return right;
    }

    // ------------------------------------------------------------- Cartes KPI

    private Control BuildKpiRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Palette.Background,
            Margin = Padding.Empty,
        };
        for (var i = 0; i < 4; i++)
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        row.Controls.Add(BuildKpiCard("Utilisateurs", "Total enregistré", Palette.ChartBlue, "◉",
            out _usersValue, out _usersSub), 0, 0);
        row.Controls.Add(BuildKpiCard("Utilisateurs actifs", "Actuellement connectés", Palette.ChartTeal, "✓",
            out _activeValue, out _activeSub), 1, 0);
        row.Controls.Add(BuildKpiCard("Messages", "Dernières 24 heures", Palette.ChartPurple, "✉",
            out _messagesValue, out _messagesSub), 2, 0);
        row.Controls.Add(BuildKpiCard("En ligne", "Serveur OrgChat", Palette.ChartTeal, "●",
            out _onlineValue, out _onlineSub), 3, 0);

        return row;
    }

    private static RoundedPanel BuildKpiCard(string title, string subtitle, Color accent, string glyph,
        out Label valueLabel, out Label subLabel)
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 13,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            Margin = new Padding(0, 0, 12, 0),
        };

        var badge = new RoundedPanel
        {
            Location = new Point(14, 12),
            Size = new Size(30, 30),
            CornerRadius = 8,
            FillColor = Palette.Blend(Palette.Surface, accent, 0.14f),
            BorderColor = Color.Transparent,
            ShowBorder = false,
        };
        var badgeGlyph = Ui.Label(glyph, new Font("Segoe UI Symbol", 11f, FontStyle.Bold), accent, autoSize: false);
        badgeGlyph.Dock = DockStyle.Fill;
        badgeGlyph.TextAlign = ContentAlignment.MiddleCenter;
        badge.Controls.Add(badgeGlyph);
        card.Controls.Add(badge);

        var caption = Ui.Label(title, Palette.SmallBold, Palette.Muted, autoSize: false);
        caption.Location = new Point(52, 13);
        caption.Size = new Size(102, 18);
        caption.AutoEllipsis = true;
        card.Controls.Add(caption);

        valueLabel = Ui.Label("—", new Font("Segoe UI", 22f, FontStyle.Bold), Palette.Text, autoSize: false);
        valueLabel.Location = new Point(16, 35);
        valueLabel.Size = new Size(130, 34);
        valueLabel.AutoEllipsis = true;
        card.Controls.Add(valueLabel);

        subLabel = Ui.Label(subtitle, Palette.Small, Palette.Faint, autoSize: false);
        subLabel.Location = new Point(16, 70);
        subLabel.Size = new Size(130, 16);
        subLabel.AutoEllipsis = true;
        card.Controls.Add(subLabel);

        return card;
    }

    private Control BuildChartsRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Palette.Background,
            Margin = Padding.Empty,
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));

        // Courbe d'activité
        var activity = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 14,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            Margin = new Padding(0, 0, 7, 0),
        };
        var activityLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(18, 14, 16, 12),
            BackColor = Color.Transparent,
        };
        activityLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        activityLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var activityHeader = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var activityTitle = Ui.Label("Activité des utilisateurs", Palette.BodyBold, Palette.Text);
        activityTitle.Location = new Point(0, 2);
        activityHeader.Controls.Add(activityTitle);
        _activityPeriodLabel = Ui.Label(string.Empty, Palette.Small, Palette.Faint, autoSize: false);
        _activityPeriodLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _activityPeriodLabel.TextAlign = ContentAlignment.MiddleRight;
        _activityPeriodLabel.Location = new Point(300, 4);
        _activityPeriodLabel.Size = new Size(150, 18);
        activityHeader.Controls.Add(_activityPeriodLabel);
        activityHeader.Resize += (_, _) =>
        {
            _activityPeriodLabel.Left = Math.Max(0, activityHeader.ClientSize.Width - _activityPeriodLabel.Width);
        };
        activityLayout.Controls.Add(activityHeader, 0, 0);

        _activityChart = new LineChartPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 0) };
        activityLayout.Controls.Add(_activityChart, 0, 1);
        activity.Controls.Add(activityLayout);
        row.Controls.Add(activity, 0, 0);

        // Donut départements
        var donutCard = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 14,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            Margin = new Padding(7, 0, 0, 0),
        };
        var donutLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(18, 14, 16, 12),
            BackColor = Color.Transparent,
        };
        donutLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        donutLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var donutHeader = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var donutTitle = Ui.Label("Utilisateurs par département", Palette.BodyBold, Palette.Text, autoSize: false);
        donutTitle.Location = new Point(0, 2);
        donutTitle.Size = new Size(300, 22);
        donutHeader.Controls.Add(donutTitle);
        var donutSub = Ui.Label("Répartition des effectifs", Palette.Small, Palette.Faint, autoSize: false);
        donutSub.Location = new Point(0, 22);
        donutSub.Size = new Size(260, 16);
        donutHeader.Controls.Add(donutSub);
        donutLayout.Controls.Add(donutHeader, 0, 0);

        _departmentDonut = new DonutChartPanel { Dock = DockStyle.Fill };
        donutLayout.Controls.Add(_departmentDonut, 0, 1);
        donutCard.Controls.Add(donutLayout);
        row.Controls.Add(donutCard, 1, 0);

        return row;
    }

    private Control BuildMetricsRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Palette.Background,
            Margin = Padding.Empty,
        };
        for (var i = 0; i < 4; i++)
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        row.Controls.Add(BuildMetricCard("Groupes", "Groupes actifs", Palette.ChartBlue, "▣", out _groupsValue), 0, 0);
        row.Controls.Add(BuildMetricCard("Messages (24 h)", "Trafic récent", Palette.ChartTeal, "✉", out _messages24Value), 1, 0);
        row.Controls.Add(BuildMetricCard("Incidents (7 j)", "À surveiller", Palette.ChartRed, "!", out _incidentsValue), 2, 0);
        row.Controls.Add(BuildMetricCard("Départements", "Organisation", Palette.ChartOrange, "▭", out _departmentsValue), 3, 0);

        return row;
    }

    private static RoundedPanel BuildMetricCard(string title, string subtitle, Color accent, string glyph,
        out Label valueLabel)
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 13,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            Margin = new Padding(0, 0, 12, 0),
        };

        var badge = new RoundedPanel
        {
            Location = new Point(14, 18),
            Size = new Size(30, 30),
            CornerRadius = 8,
            FillColor = Palette.Blend(Palette.Surface, accent, 0.14f),
            BorderColor = Color.Transparent,
            ShowBorder = false,
        };
        var badgeGlyph = Ui.Label(glyph, new Font("Segoe UI Symbol", 10.5f, FontStyle.Bold), accent, autoSize: false);
        badgeGlyph.Dock = DockStyle.Fill;
        badgeGlyph.TextAlign = ContentAlignment.MiddleCenter;
        badge.Controls.Add(badgeGlyph);
        card.Controls.Add(badge);

        valueLabel = Ui.Label("—", new Font("Segoe UI", 18f, FontStyle.Bold), Palette.Text, autoSize: false);
        valueLabel.Location = new Point(52, 6);
        valueLabel.Size = new Size(102, 28);
        valueLabel.AutoEllipsis = true;
        card.Controls.Add(valueLabel);

        var caption = Ui.Label(title, Palette.SmallBold, Palette.Text, autoSize: false);
        caption.Location = new Point(52, 35);
        caption.Size = new Size(102, 18);
        caption.AutoEllipsis = true;
        card.Controls.Add(caption);

        var sub = Ui.Label(subtitle, Palette.Small, Palette.Faint, autoSize: false);
        sub.Location = new Point(52, 54);
        sub.Size = new Size(102, 16);
        sub.AutoEllipsis = true;
        card.Controls.Add(sub);

        return card;
    }

    private Control BuildBottomRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Palette.Background,
            Margin = Padding.Empty,
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37f));

        row.Controls.Add(BuildDepartmentsCard(), 0, 0);
        row.Controls.Add(BuildQuickActionsCard(), 1, 0);
        return row;
    }

    private Control BuildDepartmentsCard()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 14,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            Margin = new Padding(0, 0, 7, 0),
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18, 14, 16, 14),
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var title = Ui.Label("Départements", Palette.BodyBold, Palette.Text, autoSize: false);
        title.Location = new Point(0, 2);
        title.Size = new Size(220, 22);
        header.Controls.Add(title);
        var subtitle = Ui.Label("Liste des équipes de l'organisation", Palette.Small, Palette.Faint, autoSize: false);
        subtitle.Location = new Point(0, 23);
        subtitle.Size = new Size(320, 16);
        header.Controls.Add(subtitle);

        var manage = Ui.Outline("Gérer", 78, 32);
        manage.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        manage.Location = new Point(340, 4);
        manage.Click += OnManageDepartmentsClicked;
        header.Controls.Add(manage);
        header.Resize += (_, _) => manage.Left = Math.Max(0, header.ClientSize.Width - manage.Width);
        layout.Controls.Add(header, 0, 0);

        _departmentsView = new ThemedListView
        {
            Dock = DockStyle.Fill,
            BackColor = Palette.Surface,
            ForeColor = Palette.Text,
            Font = Palette.Body,
            BorderStyle = BorderStyle.None,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            MultiSelect = false,
            GridLines = false,
            FullRowSelect = true,
        };
        _departmentsView.Columns.Add("Département", 140);
        _departmentsView.Columns.Add("Responsable", 130);
        _departmentsView.Columns.Add("Membres", 78);
        _departmentsView.DoubleClick += OnManageDepartmentsClicked;
        layout.Controls.Add(_departmentsView, 0, 1);

        _departmentsEmpty = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Palette.Surface,
            Visible = false,
            Margin = Padding.Empty,
        };
        var emptyIcon = Ui.Label("▭", new Font("Segoe UI Symbol", 20f), Palette.Faint, autoSize: false);
        emptyIcon.TextAlign = ContentAlignment.MiddleCenter;
        var emptyTitle = Ui.Label("Aucun département", Palette.BodyBold, Palette.Text, autoSize: false);
        emptyTitle.TextAlign = ContentAlignment.MiddleCenter;
        var emptySub = Ui.Label("Créez votre première équipe pour organiser vos utilisateurs.",
            Palette.Small, Palette.Muted, autoSize: false);
        emptySub.TextAlign = ContentAlignment.MiddleCenter;
        var emptyButton = Ui.Primary("Créer un département", 190, 32);
        emptyButton.Click += OnManageDepartmentsClicked;
        _departmentsEmpty.Controls.Add(emptyIcon);
        _departmentsEmpty.Controls.Add(emptyTitle);
        _departmentsEmpty.Controls.Add(emptySub);
        _departmentsEmpty.Controls.Add(emptyButton);

        void LayoutEmptyDepartment()
        {
            var contentHeight = 30 + 4 + 20 + 2 + 16 + 8 + 32;
            var top = Math.Max(2, (_departmentsEmpty.ClientSize.Height - contentHeight) / 2);
            emptyIcon.SetBounds(0, top, Math.Max(100, _departmentsEmpty.ClientSize.Width), 30);
            emptyTitle.SetBounds(8, top + 34, Math.Max(100, _departmentsEmpty.ClientSize.Width - 16), 20);
            emptySub.SetBounds(8, top + 56, Math.Max(100, _departmentsEmpty.ClientSize.Width - 16), 16);
            emptyButton.Left = Math.Max(0, (_departmentsEmpty.ClientSize.Width - emptyButton.Width) / 2);
            emptyButton.Top = top + 80;
        }

        _departmentsEmpty.Resize += (_, _) => LayoutEmptyDepartment();
        LayoutEmptyDepartment();
        layout.Controls.Add(_departmentsEmpty, 0, 1);
        _departmentsEmpty.BringToFront();

        var footer = Ui.Label(string.Empty, Palette.Small, Palette.Muted, autoSize: false);
        footer.Dock = DockStyle.Fill;
        footer.TextAlign = ContentAlignment.MiddleLeft;
        footer.Name = "DepartmentsFooter";
        layout.Controls.Add(footer, 0, 2);

        card.Controls.Add(layout);
        return card;
    }

    private Control BuildQuickActionsCard()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 14,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            Margin = new Padding(7, 0, 0, 0),
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(18, 10, 18, 10),
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var title = Ui.Label("Actions rapides", Palette.BodyBold, Palette.Text, autoSize: false);
        title.Dock = DockStyle.Fill;
        layout.Controls.Add(title, 0, 0);

        var subtitle = Ui.Label("Gagnez du temps au quotidien.", Palette.Small, Palette.Faint, autoSize: false);
        subtitle.Dock = DockStyle.Fill;
        layout.Controls.Add(subtitle, 0, 1);

        var createUser = Ui.Primary("＋  Créer un utilisateur", 250, 40);
        createUser.Dock = DockStyle.Fill;
        createUser.Enabled = CanAdmin;
        createUser.Click += OnCreateUserClicked;
        layout.Controls.Add(createUser, 0, 2);

        var createDepartment = Ui.Outline("▭  Nouveau département", 250, 40);
        createDepartment.Dock = DockStyle.Fill;
        createDepartment.Click += OnManageDepartmentsClicked;
        layout.Controls.Add(createDepartment, 0, 3);

        var openMessaging = Ui.Outline("✉  Ouvrir la messagerie", 250, 40);
        openMessaging.Dock = DockStyle.Fill;
        openMessaging.Click += (_, _) => OpenMessagingRequested?.Invoke(this, EventArgs.Empty);
        layout.Controls.Add(openMessaging, 0, 4);

        var supervision = Ui.Outline("↗  Ouvrir la supervision", 250, 40);
        supervision.Dock = DockStyle.Fill;
        supervision.Enabled = CanSupervise;
        supervision.Click += OnSupervisionClicked;
        layout.Controls.Add(supervision, 0, 5);

        card.Controls.Add(layout);
        return card;
    }

    private Control BuildMessagingCard()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 14,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16, 14, 16, 14),
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var title = Ui.Label("Messagerie", Palette.BodyBold, Palette.Text, autoSize: false);
        title.Location = new Point(0, 2);
        title.Size = new Size(180, 22);
        header.Controls.Add(title);

        _onlineLabel = Ui.Label("Temps réel", Palette.Small, Palette.Success, autoSize: false);
        _onlineLabel.Location = new Point(0, 23);
        _onlineLabel.Size = new Size(300, 16);
        header.Controls.Add(_onlineLabel);

        var open = Ui.Outline("Tout voir", 88, 32);
        open.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        open.Location = new Point(220, 2);
        open.Click += (_, _) => OpenMessagingRequested?.Invoke(this, EventArgs.Empty);
        header.Controls.Add(open);
        header.Resize += (_, _) => open.Left = Math.Max(0, header.ClientSize.Width - open.Width);
        layout.Controls.Add(header, 0, 0);

        var search = Ui.SearchField("Rechercher une conversation…", out _conversationSearch, height: 38);
        search.Dock = DockStyle.Fill;
        search.Margin = new Padding(0, 2, 0, 6);
        _conversationSearch.TextChanged += (_, _) => RebuildConversationList();
        layout.Controls.Add(search, 0, 1);

        _conversationList = new SmoothFlowPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Palette.Surface,
            Padding = new Padding(0, 2, 0, 2),
        };
        _conversationList.Resize += (_, _) => ResizeConversationRows();
        layout.Controls.Add(_conversationList, 0, 2);

        var openButton = Ui.Primary("Ouvrir la messagerie", 250, 40);
        openButton.Dock = DockStyle.Fill;
        openButton.Click += (_, _) => OpenMessagingRequested?.Invoke(this, EventArgs.Empty);
        layout.Controls.Add(openButton, 0, 3);

        card.Controls.Add(layout);
        return card;
    }

    private Control BuildSupervisionCard()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 14,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18, 14, 18, 14),
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var title = Ui.Label("Supervision", Palette.BodyBold, Palette.Text, autoSize: false);
        title.Location = new Point(0, 2);
        title.Size = new Size(180, 22);
        header.Controls.Add(title);

        _healthPill = new RoundedPanel
        {
            Width = 118,
            Height = 26,
            CornerRadius = 13,
            FillColor = Palette.Blend(Palette.Surface, Palette.Success, 0.16f),
            BorderColor = Color.Transparent,
            ShowBorder = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(180, 2),
        };
        _healthPillLabel = Ui.Label("Opérationnel", Palette.SmallBold, Palette.Success, autoSize: false);
        _healthPillLabel.Dock = DockStyle.Fill;
        _healthPillLabel.TextAlign = ContentAlignment.MiddleCenter;
        _healthPill.Controls.Add(_healthPillLabel);
        header.Controls.Add(_healthPill);
        header.Resize += (_, _) => _healthPill.Left = Math.Max(0, header.ClientSize.Width - _healthPill.Width);
        layout.Controls.Add(header, 0, 0);

        var stats = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
        };
        for (var i = 0; i < 3; i++)
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        stats.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        stats.Controls.Add(BuildMiniStat("Connexions", "Actives", Palette.ChartBlue, out _connValue), 0, 0);
        stats.Controls.Add(BuildMiniStat("Conversations", "Ouvertes", Palette.ChartTeal, out _convValue), 1, 0);
        stats.Controls.Add(BuildMiniStat("Incidents", "7 jours", Palette.ChartRed, out _incidentsSupervisionValue), 2, 0);
        layout.Controls.Add(stats, 0, 1);

        var open = Ui.Outline("Ouvrir la supervision", 220, 36);
        open.Dock = DockStyle.Fill;
        open.Enabled = CanSupervise;
        open.Click += OnSupervisionClicked;
        layout.Controls.Add(open, 0, 2);

        card.Controls.Add(layout);
        return card;
    }

    private static Control BuildMiniStat(string title, string subtitle, Color accent, out Label valueLabel)
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        valueLabel = Ui.Label("—", new Font("Segoe UI", 17f, FontStyle.Bold), accent, autoSize: false);
        valueLabel.Location = new Point(0, 2);
        valueLabel.Size = new Size(94, 28);
        valueLabel.AutoEllipsis = true;
        host.Controls.Add(valueLabel);

        var caption = Ui.Label(title, Palette.SmallBold, Palette.Text, autoSize: false);
        caption.Location = new Point(0, 30);
        caption.Size = new Size(94, 17);
        caption.AutoEllipsis = true;
        host.Controls.Add(caption);

        var sub = Ui.Label(subtitle, Palette.Small, Palette.Faint, autoSize: false);
        sub.Location = new Point(0, 47);
        sub.Size = new Size(94, 16);
        sub.AutoEllipsis = true;
        host.Controls.Add(sub);

        return host;
    }

    // ---------------------------------------------------------------- Données

    private async Task LoadAllAsync()
    {
        if (_busy)
            return;

        _busy = true;
        UseWaitCursor = true;
        _loadingLabel.Text = Localization.T("Chargement des statistiques…");
        _errorLabel.Text = string.Empty;

        Exception? firstError = null;

        async Task<T> Load<T>(Func<Task<T>> action, T fallback)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                firstError ??= ex;
                return fallback;
            }
        }

        try
        {
            _users = await Load(() => _api.GetUsersAsync(), new List<UserDto>());
            _departments = await Load(() => _api.GetDepartmentsAsync(), new List<DepartmentDto>());
            _conversations = await Load(() => _api.GetConversationsAsync(), new List<ConversationDto>());

            var unread = await Load(() => _api.GetNotificationUnreadCountAsync(), new NotificationCountDto());
            _notificationUnreadCount = unread.UnreadCount;

            if (CanSupervise)
            {
                _summary = await Load(() => _api.GetSupervisionSummaryAsync(), null!);
                _activity = await Load(() => _api.GetSupervisionActivityAsync(14), new List<ActivityPointDto>());
                var incidents = await Load(
                    () => _api.GetSupervisionIncidentsAsync(7, 1, 1),
                    new PagedResultDto<IncidentDto>());
                _incidentCount = incidents.TotalCount;
                _health = await Load(() => _api.GetSupervisionHealthAsync(), null!);
            }

            UpdateDashboard();

            if (firstError != null)
                _errorLabel.Text = Ui.FriendlyError(firstError);
        }
        catch (Exception ex)
        {
            _errorLabel.Text = Ui.FriendlyError(ex);
        }
        finally
        {
            _busy = false;
            UseWaitCursor = false;
            _loadingLabel.Text = string.Empty;
        }
    }

    private void UpdateDashboard()
    {
        var totalUsers = _summary?.TotalUsers ?? _users.Count;
        var activeUsers = _summary?.ActiveUsers ?? _users.Count(u => u.IsActive);
        var messages = _summary?.MessagesLast24Hours ?? 0;
        var onlineUsers = _summary?.OnlineUsers ?? _users.Count(u => u.IsOnline);

        _usersValue.Text = totalUsers.ToString("N0");
        _usersSub.Text = "Total enregistré";

        _activeValue.Text = activeUsers.ToString("N0");
        _activeSub.Text = totalUsers > 0
            ? $"{activeUsers * 100d / totalUsers:0}% de l'effectif"
            : "Actuellement connectés";

        _messagesValue.Text = messages.ToString("N0");
        _messagesSub.Text = "Dernières 24 heures";

        _onlineValue.Text = onlineUsers.ToString("N0");
        var healthy = _health is null || string.Equals(_health.Status, "Healthy", StringComparison.OrdinalIgnoreCase);
        _onlineSub.Text = healthy ? "Serveur opérationnel" : "Serveur à vérifier";
        _onlineSub.ForeColor = healthy ? Palette.Success : Palette.Warning;

        var groups = _conversations.Count(c => c.IsGroup);
        _groupsValue.Text = groups.ToString("N0");
        _messages24Value.Text = messages.ToString("N0");
        _incidentsValue.Text = _incidentCount.ToString("N0");
        _departmentsValue.Text = _departments.Count.ToString("N0");

        UpdateActivityChart();
        UpdateDepartmentDonut();
        UpdateDepartmentsList();
        UpdateMessagingPreview();
        UpdateSupervisionSummary();

        var from = DateTime.Today.AddDays(-13);
        _dateRangeLabel.Text = $"{from:dd/MM/yyyy} – {DateTime.Today:dd/MM/yyyy}";
        _notificationsButton.Text = _notificationUnreadCount > 0 ? $"🔔 {_notificationUnreadCount}" : "🔔";
        _onlineLabel.Text = onlineUsers > 0
            ? $"●  {onlineUsers} utilisateur{(onlineUsers > 1 ? "s" : string.Empty)} en ligne"
            : "●  Temps réel";
        _onlineLabel.ForeColor = onlineUsers > 0 ? Palette.Success : Palette.Muted;
    }

    private void UpdateActivityChart()
    {
        if (_activity.Count == 0)
        {
            _activityPeriodLabel.Text = string.Empty;
            _activityChart.EmptyText = CanSupervise
                ? "Aucune activité sur la période."
                : "Données de supervision réservées.";
            _activityChart.SetSeries();
            return;
        }

        var logins = _activity
            .Select(p => new ChartPoint(p.Date.ToString("dd/MM"), p.Logins))
            .ToList();
        var actives = _activity
            .Select(p => new ChartPoint(p.Date.ToString("dd/MM"), p.ActiveUsers))
            .ToList();

        _activityChart.SetSeries(
            new ChartSeries("Connexions", Palette.ChartBlue, logins),
            new ChartSeries("Utilisateurs actifs", Palette.ChartTeal, actives));

        _activityPeriodLabel.Text = $"{_activity[0].Date:dd/MM} – {_activity[^1].Date:dd/MM}";
    }

    private void UpdateDepartmentDonut()
    {
        var segments = new List<ChartSegment>();
        var colors = new[]
        {
            Palette.ChartBlue, Palette.ChartTeal, Palette.ChartOrange,
            Palette.ChartPurple, Palette.ChartNavy, Palette.ChartSlate,
        };

        var ordered = _departments
            .Where(d => d.MemberCount > 0)
            .OrderByDescending(d => d.MemberCount)
            .ToList();

        if (ordered.Count == 0)
        {
            var roles = _users
                .GroupBy(u => u.Role)
                .Select(g => new { Label = RoleShortLabel(g.Key), Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .ToList();

            for (var i = 0; i < roles.Count; i++)
                segments.Add(new ChartSegment(roles[i].Label, roles[i].Count, colors[i % colors.Length]));
        }
        else
        {
            var top = ordered.Take(5).ToList();
            var other = ordered.Skip(5).Sum(d => d.MemberCount);
            for (var i = 0; i < top.Count; i++)
                segments.Add(new ChartSegment(top[i].Name, top[i].MemberCount, colors[i % colors.Length]));
            if (other > 0)
                segments.Add(new ChartSegment("Autres", other, Palette.ChartSlate));
        }

        _departmentDonut.SetSegments(segments);
    }

    private void UpdateDepartmentsList()
    {
        _departmentsView.BeginUpdate();
        _departmentsView.Items.Clear();
        foreach (var department in _departments
                     .OrderByDescending(d => d.MemberCount)
                     .ThenBy(d => d.Name)
                     .Take(12))
        {
            var item = new ListViewItem(department.Name);
            item.SubItems.Add(department.ManagerName ?? "—");
            item.SubItems.Add(department.MemberCount.ToString("N0"));
            _departmentsView.Items.Add(item);
        }
        _departmentsView.EndUpdate();

        var empty = _departments.Count == 0;
        _departmentsView.Visible = !empty;
        _departmentsEmpty.Visible = empty;
        if (empty)
            _departmentsEmpty.BringToFront();

        if (_departmentsView.Parent is Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child.Name == "DepartmentsFooter")
                {
                    child.Text = _departments.Count == 0
                        ? "0 département"
                        : $"{_departments.Count} département{(_departments.Count > 1 ? "s" : string.Empty)} · " +
                          $"{_departments.Sum(d => d.MemberCount):N0} affectations";
                }
            }
        }
    }

    private void UpdateMessagingPreview()
    {
        RebuildConversationList();
    }

    private void RebuildConversationList()
    {
        if (_conversationList is null)
            return;

        _conversationList.SuspendLayout();
        _conversationList.Controls.Clear();

        var term = (_conversationSearch?.Text ?? string.Empty).Trim();
        var conversations = _conversations
            .Where(c => term.Length == 0 ||
                        c.DisplayTitle(_auth.UserId).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        c.PreviewText.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .Take(8)
            .ToList();

        if (conversations.Count == 0)
        {
            var empty = BuildEmptyConversationPanel(term.Length > 0);
            empty.Width = Math.Max(140, _conversationList.ClientSize.Width - 8);
            empty.Height = Math.Max(170, _conversationList.ClientSize.Height - 8);
            _conversationList.Controls.Add(empty);
        }
        else
        {
            foreach (var conversation in conversations)
            {
                var row = new DashboardConversationRow(conversation, _auth.UserId)
                {
                    Width = Math.Max(140, _conversationList.ClientSize.Width - 8),
                    Height = 64,
                    Margin = new Padding(0, 0, 0, 4),
                };
                row.Activate += (_, _) => OpenMessagingRequested?.Invoke(this, EventArgs.Empty);
                _conversationList.Controls.Add(row);
            }
        }

        _conversationList.ResumeLayout();
    }

    private Control BuildEmptyConversationPanel(bool filtered)
    {
        var panel = new Panel
        {
            BackColor = Palette.Surface,
            Margin = Padding.Empty,
        };

        var mark = new BrandMark { Size = new Size(52, 52) };
        var title = Ui.Label(filtered ? "Aucun résultat" : "Aucune conversation",
            Palette.BodyBold, Palette.Text, autoSize: false);
        title.TextAlign = ContentAlignment.MiddleCenter;
        var subtitle = Ui.Label(
            filtered ? "Essayez un autre mot-clé." : "Démarrez un échange avec vos collègues.",
            Palette.Small,
            Palette.Muted,
            autoSize: false);
        subtitle.TextAlign = ContentAlignment.MiddleCenter;

        var button = Ui.Primary("Nouvelle conversation", 200, 38);
        button.Click += (_, _) => OpenMessagingRequested?.Invoke(this, EventArgs.Empty);

        panel.Controls.Add(mark);
        panel.Controls.Add(title);
        panel.Controls.Add(subtitle);
        panel.Controls.Add(button);

        void LayoutEmpty()
        {
            var contentHeight = 52 + 10 + 24 + 4 + 18 + 14 + 38;
            var top = Math.Max(6, (panel.ClientSize.Height - contentHeight) / 2);
            mark.Left = Math.Max(0, (panel.ClientSize.Width - mark.Width) / 2);
            mark.Top = top;
            title.SetBounds(8, top + 62, Math.Max(100, panel.ClientSize.Width - 16), 24);
            subtitle.SetBounds(8, top + 88, Math.Max(100, panel.ClientSize.Width - 16), 18);
            button.Left = Math.Max(0, (panel.ClientSize.Width - button.Width) / 2);
            button.Top = top + 118;
        }

        panel.Resize += (_, _) => LayoutEmpty();
        LayoutEmpty();
        return panel;
    }

    private void ResizeConversationRows()
    {
        var width = Math.Max(140, _conversationList.ClientSize.Width - 8);
        foreach (Control control in _conversationList.Controls)
        {
            control.Width = width;
            if (control is Panel panel)
                panel.Height = Math.Max(170, _conversationList.ClientSize.Height - 8);
        }
    }

    private void UpdateSupervisionSummary()
    {
        _connValue.Text = (_summary?.ConcurrentConnections ?? _summary?.OnlineUsers ?? 0).ToString("N0");
        _convValue.Text = (_summary?.Conversations ?? _conversations.Count).ToString("N0");
        _incidentsSupervisionValue.Text = _incidentCount.ToString("N0");

        var (text, color) = _health?.Status.ToLowerInvariant() switch
        {
            "healthy" => ("Opérationnel", Palette.Success),
            "degraded" => ("Dégradé", Palette.Warning),
            null => ("Indisponible", Palette.Muted),
            _ => (_health!.Status, Palette.Warning),
        };

        _healthPillLabel.Text = text;
        _healthPillLabel.ForeColor = color;
        _healthPill.FillColor = Palette.Blend(Palette.Surface, color, 0.16f);
        _healthPill.Invalidate();
    }

    // --------------------------------------------------------------- Actions

    private async Task OpenProfileAsync()
    {
        try
        {
            var me = await _api.GetMeAsync();
            using var form = new ProfileForm(_api, me);
            if (form.ShowDialog(this) == DialogResult.OK && form.UpdatedUser != null)
            {
                _auth.DisplayName = form.UpdatedUser.DisplayName;
                _auth.Email = form.UpdatedUser.Email;
            }
        }
        catch (Exception ex)
        {
            _errorLabel.Text = Ui.FriendlyError(ex);
        }
    }

    private async void OnManageUsersClicked(object? sender, EventArgs e)
    {
        try
        {
            var users = await _api.GetUsersAsync();
            using var form = new UsersForm(_api, users, _auth.UserId, _auth.IsAdmin);
            form.ShowDialog(this);
            _ = LoadAllAsync();
        }
        catch (Exception ex)
        {
            _errorLabel.Text = Ui.FriendlyError(ex);
        }
    }

    private async void OnCreateUserClicked(object? sender, EventArgs e)
    {
        try
        {
            using var form = new CreateUserForm(_api);
            form.ShowDialog(this);
            _ = LoadAllAsync();
        }
        catch (Exception ex)
        {
            _errorLabel.Text = Ui.FriendlyError(ex);
        }
    }

    private void OnManageDepartmentsClicked(object? sender, EventArgs e)
    {
        using var form = new DepartmentsForm(_api, _auth);
        form.ShowDialog(this);
        _ = LoadAllAsync();
    }

    private void OnSupervisionClicked(object? sender, EventArgs e)
    {
        if (!CanSupervise)
            return;

        using var form = new SupervisionForm(_api, _auth);
        form.ShowDialog(this);
        _ = LoadAllAsync();
    }

    private void OnNotificationsClicked(object? sender, EventArgs e)
    {
        using var form = new NotificationsForm(_api);
        form.ShowDialog(this);
        _ = LoadAllAsync();
    }

    private static string RoleLabel(string roleKey) => roleKey switch
    {
        "SuperAdmin" => "Super administrateur",
        "OrgAdmin" => "Administrateur d'organisation",
        "Supervisor" => "Superviseur",
        "DepartmentManager" => "Responsable de département",
        _ => "Utilisateur",
    };

    private static string RoleShortLabel(string roleKey) => roleKey switch
    {
        "SuperAdmin" => "SuperAdmin",
        "OrgAdmin" => "Admin org.",
        "Supervisor" => "Superviseur",
        "DepartmentManager" => "Resp. dépt",
        _ => "Utilisateurs",
    };

    /// <summary>Ligne compacte d'aperçu de conversation sur fond clair.</summary>
    private sealed class DashboardConversationRow : Control
    {
        private bool _hovered;

        public DashboardConversationRow(ConversationDto conversation, int currentUserId)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Conversation = conversation;
            CurrentUserId = currentUserId;
            Cursor = Cursors.Hand;
            BackColor = Palette.Surface;
        }

        public ConversationDto Conversation { get; }

        public int CurrentUserId { get; }

        public event EventHandler? Activate;

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Activate?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var background = new SolidBrush(_hovered ? Palette.SurfaceHover : Palette.Surface))
                g.FillRectangle(background, ClientRectangle);

            using (var border = new Pen(Palette.WithAlpha(Palette.Border, 120)))
                g.DrawLine(border, 0, Height - 1, Width, Height - 1);

            AvatarControl.DrawAvatar(g, new Rectangle(4, 11, 38, 38),
                Conversation.DisplayTitle(CurrentUserId), Conversation.Id.ToString(), showStatus: false);

            var title = Conversation.DisplayTitle(CurrentUserId);
            var time = Ui.FormatConversationTime(Conversation.LastMessageAt ?? Conversation.CreatedAt);
            var timeWidth = string.IsNullOrEmpty(time) ? 0 : TextRenderer.MeasureText(time, Palette.Small).Width;

            var titleRect = new Rectangle(52, 9, Math.Max(30, Width - 52 - timeWidth - 20), 20);
            TextRenderer.DrawText(g, title, Palette.BodyBold, titleRect, Palette.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (timeWidth > 0)
            {
                TextRenderer.DrawText(g, time, Palette.Small,
                    new Rectangle(Width - timeWidth - 6, 11, timeWidth, 16), Palette.Faint,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }

            var previewWidth = Math.Max(30, Width - 52 - 16);
            if (Conversation.UnreadCount > 0)
            {
                var badgeText = Conversation.UnreadCount > 99 ? "99+" : Conversation.UnreadCount.ToString();
                var badgeWidth = Math.Max(20, TextRenderer.MeasureText(badgeText, Palette.SmallBold).Width + 12);
                var badge = new Rectangle(Width - badgeWidth - 6, 32, badgeWidth, 18);
                Palette.DrawRounded(g, badge, 9, Palette.Accent);
                TextRenderer.DrawText(g, badgeText, Palette.SmallBold, badge, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                previewWidth = Math.Max(30, badge.Left - 10 - 52);
            }

            TextRenderer.DrawText(g, Conversation.PreviewText, Palette.Small,
                new Rectangle(52, 31, previewWidth, 18),
                Conversation.LastMessagePreview is null ? Palette.Faint : Palette.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
