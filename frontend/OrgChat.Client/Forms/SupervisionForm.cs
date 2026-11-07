using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>
/// Module de supervision professionnel : vue d'ensemble, utilisateurs,
/// activité, départements, rapports, journal d'audit et santé du service.
/// Toutes les données affichées sont des métadonnées : le contenu des
/// conversations privées n'est jamais demandé ni affiché.
/// </summary>
public sealed class SupervisionForm : Form
{
    private enum Page
    {
        Overview,
        Users,
        Activity,
        Departments,
        Reports,
        Audit,
        Health,
    }

    private readonly ApiClient _api;
    private readonly AuthResponse _auth;

    private readonly Panel _pageHost = new() { Dock = DockStyle.Fill, BackColor = Palette.Background };
    private readonly Dictionary<Page, Control> _pages = new();
    private readonly Dictionary<Page, RoundedButton> _navButtons = new();
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly Label _loadingLabel;
    private readonly Label _errorLabel;

    private Page _current = Page.Overview;
    private bool _busy;

    // Vue d'ensemble
    private Label _ovUsers = null!;
    private Label _ovActive = null!;
    private Label _ovOnline = null!;
    private Label _ovSessions = null!;
    private BarChartPanel _ovChart = null!;
    private DonutChartPanel _ovDonut = null!;
    private ListView _ovIncidents = null!;
    private ListView _ovDepartments = null!;

    // Utilisateurs
    private TextBox _usersSearch = null!;
    private ComboBox _usersStatus = null!;
    private ComboBox _usersRole = null!;
    private ListView _usersView = null!;
    private Label _usersPageLabel = null!;
    private RoundedButton _usersPrev = null!;
    private RoundedButton _usersNext = null!;
    private int _usersPage = 1;
    private int _usersPages = 1;

    // Activité
    private ComboBox _activityRange = null!;
    private BarChartPanel _activityMessages = null!;
    private BarChartPanel _activityLogins = null!;
    private ListView _activityDetail = null!;
    private Label _activityConnections = null!;

    // Départements
    private ListView _departmentsView = null!;

    // Rapports
    private ComboBox _reportPeriod = null!;
    private Label _reportRange = null!;
    private Label _reportUsers = null!;
    private Label _reportActive = null!;
    private Label _reportLogins = null!;
    private Label _reportMessages = null!;
    private Label _reportConversations = null!;
    private Label _reportIncidents = null!;
    private ListView _reportTop = null!;
    private ListView _reportDepartments = null!;

    // Audit
    private TextBox _auditSearch = null!;
    private ComboBox _auditAction = null!;
    private ComboBox _auditResult = null!;
    private ListView _auditView = null!;
    private Label _auditPageLabel = null!;
    private RoundedButton _auditPrev = null!;
    private RoundedButton _auditNext = null!;
    private int _auditPage = 1;
    private int _auditPages = 1;
    private bool _auditFacetsLoaded;

    // Santé
    private Label _healthApi = null!;
    private Label _healthDatabase = null!;
    private Label _healthSignalR = null!;
    private Label _healthSessions = null!;
    private Label _healthDetails = null!;

    public SupervisionForm(ApiClient api, AuthResponse auth)
    {
        _api = api;
        _auth = auth;

        Ui.StyleForm(this);
        Text = "OrgChat · Supervision";
        ClientSize = new Size(1200, 780);
        MinimumSize = new Size(1120, 720);
        DoubleBuffered = true;

        _titleLabel = Ui.Label("Supervision", Palette.Heading, Palette.Text, autoSize: false);
        _subtitleLabel = Ui.Label(string.Empty, Palette.Small, Palette.Muted, autoSize: false);
        _loadingLabel = Ui.Label(string.Empty, Palette.Small, Palette.Muted, autoSize: false);
        _errorLabel = Ui.Label(string.Empty, Palette.Small, Palette.Danger, autoSize: false);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            BackColor = Palette.Background,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(BuildMain(), 1, 0);
        Controls.Add(root);

        Localization.Apply(this);
        _ = ShowPageAsync(Page.Overview);
    }

    // ------------------------------------------------------------------ Coquille

    private Panel BuildSidebar()
    {
        var sidebar = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 0,
            ShowBorder = false,
            FillColor = Palette.Sidebar,
        };

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16, 18, 16, 16),
            BackColor = Color.Transparent,
        };
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var brandHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var mark = new BrandMark { Location = new Point(0, 2) };
        brandHost.Controls.Add(mark);
        var brand = Ui.Label("OrgChat", new Font("Segoe UI", 15f, FontStyle.Bold), Palette.Text, autoSize: false);
        brand.Location = new Point(46, 2);
        brand.Size = new Size(150, 30);
        brandHost.Controls.Add(brand);
        var subBrand = Ui.Label("Supervision", Palette.Small, Palette.Muted, autoSize: false);
        subBrand.Location = new Point(46, 34);
        brandHost.Controls.Add(subBrand);
        stack.Controls.Add(brandHost, 0, 0);

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
        };
        nav.Controls.Add(CreateNavButton("▦  Vue d'ensemble", Page.Overview));
        nav.Controls.Add(CreateNavButton("◉  Utilisateurs", Page.Users));
        nav.Controls.Add(CreateNavButton("↗  Activité", Page.Activity));
        nav.Controls.Add(CreateNavButton("▭  Départements", Page.Departments));
        nav.Controls.Add(CreateNavButton("▤  Rapports", Page.Reports));
        nav.Controls.Add(CreateNavButton("⛨  Journal d'audit", Page.Audit));
        nav.Controls.Add(CreateNavButton("♥  Santé du service", Page.Health));
        stack.Controls.Add(nav, 0, 1);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        var userCard = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 10,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            Margin = new Padding(0, 0, 0, 8),
        };
        var name = Ui.Label(_auth.DisplayName, Palette.SmallBold, Palette.Text, autoSize: false);
        name.Location = new Point(10, 6);
        name.Size = new Size(180, 18);
        userCard.Controls.Add(name);
        var role = Ui.Label(RoleLabel(_auth.Role), Palette.Small, Palette.Muted, autoSize: false);
        role.Location = new Point(10, 24);
        role.Size = new Size(180, 16);
        userCard.Controls.Add(role);
        footer.Controls.Add(userCard, 0, 0);

        var close = Ui.Outline("←  Retour au tableau de bord", 192, 40);
        close.Click += (_, _) => Close();
        footer.Controls.Add(close, 0, 1);
        stack.Controls.Add(footer, 0, 2);

        sidebar.Controls.Add(stack);
        return sidebar;
    }

    private RoundedButton CreateNavButton(string text, Page page)
    {
        var button = Ui.Outline(text, 192, 40);
        button.Margin = new Padding(0, 0, 0, 6);
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.Click += async (_, _) => await ShowPageAsync(page);
        _navButtons[page] = button;
        return button;
    }

    private Control BuildMain()
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24, 18, 24, 12),
            BackColor = Palette.Background,
        };
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Palette.Background,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));

        var titleHost = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        _titleLabel.Location = new Point(0, 0);
        _titleLabel.Size = new Size(500, 32);
        titleHost.Controls.Add(_titleLabel);
        _subtitleLabel.Location = new Point(0, 34);
        _subtitleLabel.Size = new Size(600, 20);
        titleHost.Controls.Add(_subtitleLabel);
        header.Controls.Add(titleHost, 0, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 8, 0, 0),
        };
        var refresh = Ui.Primary("Actualiser", 120, 40);
        refresh.Click += async (_, _) => await ReloadCurrentAsync();
        actions.Controls.Add(refresh);
        header.Controls.Add(actions, 1, 0);
        host.Controls.Add(header, 0, 0);

        host.Controls.Add(_pageHost, 0, 1);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Palette.Background,
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
        _loadingLabel.Dock = DockStyle.Fill;
        footer.Controls.Add(_loadingLabel, 0, 0);
        _errorLabel.Dock = DockStyle.Fill;
        _errorLabel.TextAlign = ContentAlignment.TopRight;
        footer.Controls.Add(_errorLabel, 1, 0);
        host.Controls.Add(footer, 0, 2);

        return host;
    }

    private async Task ShowPageAsync(Page page)
    {
        if (_busy)
            return;

        _current = page;
        var control = GetPage(page);
        foreach (var pair in _pages)
            pair.Value.Visible = pair.Key == page;
        control.Visible = true;

        UpdateNavStyles();
        var (title, subtitle) = TitlesFor(page);
        _titleLabel.Text = Localization.T(title);
        _subtitleLabel.Text = Localization.T(subtitle);

        await RunAsync(() => LoadPageAsync(page));
    }

    private async Task ReloadCurrentAsync()
    {
        if (_busy)
            return;
        await RunAsync(() => LoadPageAsync(_current));
    }

    private async Task RunAsync(Func<Task> load)
    {
        _busy = true;
        UseWaitCursor = true;
        _errorLabel.Text = string.Empty;
        _loadingLabel.Text = Localization.T("Chargement des données de supervision…");
        try
        {
            await load();
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

    private Task LoadPageAsync(Page page) => page switch
    {
        Page.Users => LoadUsersAsync(),
        Page.Activity => LoadActivityAsync(),
        Page.Departments => LoadDepartmentsAsync(),
        Page.Reports => LoadReportAsync(),
        Page.Audit => LoadAuditAsync(),
        Page.Health => LoadHealthAsync(),
        _ => LoadOverviewAsync(),
    };

    private void UpdateNavStyles()
    {
        foreach (var pair in _navButtons)
        {
            var active = pair.Key == _current;
            pair.Value.Outline = !active;
            pair.Value.NormalColor = Palette.AccentSoft;
            pair.Value.HoverColor = Palette.SurfaceHover;
            pair.Value.ForeColor = active ? Palette.Text : Palette.Muted;
            pair.Value.Font = active ? Palette.BodyBold : Palette.Body;
            pair.Value.Invalidate();
        }
    }

    private static (string Title, string Subtitle) TitlesFor(Page page) => page switch
    {
        Page.Users => ("Utilisateurs", "Comptes, statuts, départements et dernières connexions."),
        Page.Activity => ("Activité", "Connexions et volumes de messages — métadonnées uniquement."),
        Page.Departments => ("Départements", "Répartition des membres et de l'activité par département."),
        Page.Reports => ("Rapports", "Synthèse quotidienne, hebdomadaire et mensuelle."),
        Page.Audit => ("Journal d'audit", "Traçabilité inaltérable des actions administratives et de sécurité."),
        Page.Health => ("Santé du service", "Disponibilité de l'API, de la base de données et de SignalR."),
        _ => ("Supervision", "Vue d'ensemble de l'activité et de la santé de la plateforme."),
    };

    private Control GetPage(Page page)
    {
        if (_pages.TryGetValue(page, out var existing))
            return existing;

        Control control = page switch
        {
            Page.Users => BuildUsersPage(),
            Page.Activity => BuildActivityPage(),
            Page.Departments => BuildDepartmentsPage(),
            Page.Reports => BuildReportsPage(),
            Page.Audit => BuildAuditPage(),
            Page.Health => BuildHealthPage(),
            _ => BuildOverviewPage(),
        };

        control.Dock = DockStyle.Fill;
        control.Visible = false;
        _pages[page] = control;
        _pageHost.Controls.Add(control);
        return control;
    }

    // -------------------------------------------------------------- Composants

    private static RoundedPanel Card()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            Margin = new Padding(0, 0, 12, 0),
        };
        return card;
    }

    private static RoundedPanel MetricCard(string caption, string value, string hint, Color accent,
        out Label valueLabel)
    {
        var card = Card();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16, 12, 16, 10),
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var titleHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var dot = new Panel { BackColor = accent, Location = new Point(0, 7), Size = new Size(8, 8) };
        titleHost.Controls.Add(dot);
        var title = Ui.Label(caption.ToUpperInvariant(), Palette.SmallBold, Palette.Muted, autoSize: false);
        title.Location = new Point(16, 2);
        title.Size = new Size(220, 18);
        titleHost.Controls.Add(title);
        layout.Controls.Add(titleHost, 0, 0);

        valueLabel = Ui.Label(value, new Font("Segoe UI", 21f, FontStyle.Bold), Palette.Text, autoSize: false);
        valueLabel.Dock = DockStyle.Fill;
        layout.Controls.Add(valueLabel, 0, 1);

        var hintLabel = Ui.Label(hint, Palette.Small, Palette.Faint, autoSize: false);
        hintLabel.Dock = DockStyle.Fill;
        layout.Controls.Add(hintLabel, 0, 2);

        card.Controls.Add(layout);
        return card;
    }

    private static RoundedPanel ChartCard(string title, Control chart)
    {
        var card = Card();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16, 12, 12, 10),
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var header = Ui.Label(title, Palette.BodyBold, Palette.Text, autoSize: false);
        header.Dock = DockStyle.Fill;
        layout.Controls.Add(header, 0, 0);

        chart.Dock = DockStyle.Fill;
        layout.Controls.Add(chart, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private static RoundedPanel ListCard(string title, ListView listView)
    {
        var card = Card();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16, 12, 12, 12),
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var header = Ui.Label(title, Palette.BodyBold, Palette.Text, autoSize: false);
        header.Dock = DockStyle.Fill;
        layout.Controls.Add(header, 0, 0);

        StyleList(listView);
        listView.Dock = DockStyle.Fill;
        layout.Controls.Add(listView, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private static void StyleList(ListView listView)
    {
        listView.View = View.Details;
        listView.FullRowSelect = true;
        listView.GridLines = false;
        listView.BorderStyle = BorderStyle.None;
        listView.MultiSelect = false;
        listView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        listView.BackColor = Palette.Surface;
        listView.ForeColor = Palette.Text;
        listView.Font = Palette.Body;
        listView.HideSelection = false;
    }

    private static void SetColumns(ListView listView, params (string Title, int Width)[] columns)
    {
        listView.Columns.Clear();
        foreach (var (title, width) in columns)
            listView.Columns.Add(title, width);
    }

    private static ComboBox StyledCombo(int width)
    {
        return new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Palette.Surface,
            ForeColor = Palette.Text,
            Font = Palette.Body,
            Width = width,
            Margin = new Padding(0, 0, 10, 0),
        };
    }

    private static TextBox StyledSearch(string placeholder, int width, out RoundedPanel field)
    {
        field = Ui.Field(placeholder, out var box);
        field.Width = width;
        field.Margin = new Padding(0, 0, 10, 0);
        return box;
    }

    // -------------------------------------------------------------- Vue d'ensemble

    private Control BuildOverviewPage()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Palette.Background,
        };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 52f));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 48f));

        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Palette.Background,
        };
        for (var i = 0; i < 4; i++)
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        var usersCard = MetricCard("Utilisateurs", "—", "Comptes de l'organisation", Palette.Accent, out _ovUsers);
        usersCard.Margin = new Padding(0, 0, 12, 8);
        cards.Controls.Add(usersCard, 0, 0);

        var activeCard = MetricCard("Actifs", "—", "Comptes utilisables", Palette.Success, out _ovActive);
        activeCard.Margin = new Padding(0, 0, 12, 8);
        cards.Controls.Add(activeCard, 1, 0);

        var onlineCard = MetricCard("En ligne", "—", "Présence temps réel", Palette.Warning, out _ovOnline);
        onlineCard.Margin = new Padding(0, 0, 12, 8);
        cards.Controls.Add(onlineCard, 2, 0);

        var sessionCard = MetricCard("Sessions actives", "—", "Jetons valides", Palette.Faint, out _ovSessions);
        sessionCard.Margin = new Padding(0, 0, 0, 8);
        cards.Controls.Add(sessionCard, 3, 0);
        page.Controls.Add(cards, 0, 0);

        var charts = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Palette.Background,
            Padding = new Padding(0, 4, 0, 8),
        };
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));

        _ovChart = new BarChartPanel { BarColor = Palette.Accent };
        charts.Controls.Add(ChartCard("Messages envoyés · 14 derniers jours", _ovChart), 0, 0);

        _ovDonut = new DonutChartPanel();
        charts.Controls.Add(ChartCard("Répartition des comptes", _ovDonut), 1, 0);
        page.Controls.Add(charts, 0, 1);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Palette.Background,
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56f));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44f));

        _ovIncidents = new ThemedListView();
        SetColumns(_ovIncidents, ("Date", 120), ("Acteur", 130), ("Action", 150), ("Résultat", 80));
        bottom.Controls.Add(ListCard("Incidents récents", _ovIncidents), 0, 0);

        _ovDepartments = new ThemedListView();
        SetColumns(_ovDepartments, ("Département", 160), ("Membres", 70), ("En ligne", 70));
        bottom.Controls.Add(ListCard("Départements", _ovDepartments), 1, 0);

        page.Controls.Add(bottom, 0, 2);
        return page;
    }

    private async Task LoadOverviewAsync()
    {
        var summaryTask = _api.GetSupervisionSummaryAsync();
        var activityTask = _api.GetSupervisionActivityAsync(14);
        var incidentsTask = _api.GetSupervisionIncidentsAsync(7, 1, 6);
        var departmentsTask = _api.GetSupervisionDepartmentsAsync();
        await Task.WhenAll(summaryTask, activityTask, incidentsTask, departmentsTask);

        var summary = await summaryTask;
        _ovUsers.Text = summary.TotalUsers.ToString();
        _ovActive.Text = summary.ActiveUsers.ToString();
        _ovOnline.Text = summary.OnlineUsers.ToString();
        _ovSessions.Text = summary.ActiveSessions.ToString();

        _ovChart.SetPoints((await activityTask).Select(p =>
            new ChartPoint(p.Date.ToString("dd/MM"), p.Messages)));

        _ovDonut.SetSegments(new[]
        {
            new ChartSegment("Actifs", summary.ActiveUsers, Palette.Success),
            new ChartSegment("Suspendus", summary.SuspendedUsers, Palette.Warning),
            new ChartSegment("Désactivés", summary.DisabledUsers, Palette.Danger),
        });

        _ovIncidents.Items.Clear();
        foreach (var incident in (await incidentsTask).Items)
        {
            var item = new ListViewItem(FormatDateTime(incident.CreatedAt));
            item.SubItems.Add(incident.ActorName);
            item.SubItems.Add(Localization.T(incident.Action));
            item.SubItems.Add(Localization.T(incident.Result));
            _ovIncidents.Items.Add(item);
        }

        _ovDepartments.Items.Clear();
        foreach (var department in (await departmentsTask).Take(8))
        {
            var item = new ListViewItem(department.Name);
            item.SubItems.Add(department.MemberCount.ToString());
            item.SubItems.Add(department.OnlineUsers.ToString());
            _ovDepartments.Items.Add(item);
        }
    }

    // ------------------------------------------------------------------ Utilisateurs

    private Control BuildUsersPage()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Palette.Background,
        };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 4, 0, 4),
        };

        _usersSearch = StyledSearch("Rechercher un utilisateur…", 280, out var searchField);
        _usersSearch.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            _usersPage = 1;
            await RunAsync(LoadUsersAsync);
        };
        filters.Controls.Add(searchField);

        _usersStatus = StyledCombo(170);
        _usersStatus.Items.AddRange(new object[] { "Tous les statuts", "Actifs", "Suspendus", "Désactivés" });
        _usersStatus.SelectedIndex = 0;
        filters.Controls.Add(_usersStatus);

        _usersRole = StyledCombo(200);
        _usersRole.Items.AddRange(new object[]
        {
            "Tous les rôles", "Utilisateur", "Responsable de département",
            "Superviseur", "Administrateur d'organisation", "Super administrateur",
        });
        _usersRole.SelectedIndex = 0;
        filters.Controls.Add(_usersRole);

        var searchButton = Ui.Primary("Filtrer", 100, 40);
        searchButton.Margin = new Padding(0, 0, 0, 0);
        searchButton.Click += async (_, _) =>
        {
            _usersPage = 1;
            await RunAsync(LoadUsersAsync);
        };
        filters.Controls.Add(searchButton);
        page.Controls.Add(filters, 0, 0);

        _usersView = new ThemedListView();
        SetColumns(_usersView,
            ("Utilisateur", 140),
            ("Nom affiché", 170),
            ("Rôle", 150),
            ("Département", 150),
            ("Statut", 90),
            ("En ligne", 70),
            ("Dernière connexion", 150));
        page.Controls.Add(ListCard("Comptes", _usersView), 0, 1);

        var pager = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Palette.Background,
        };
        pager.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        pager.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        pager.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

        _usersPageLabel = Ui.Label(string.Empty, Palette.Small, Palette.Muted, autoSize: false);
        _usersPageLabel.Dock = DockStyle.Fill;
        _usersPageLabel.TextAlign = ContentAlignment.MiddleLeft;
        pager.Controls.Add(_usersPageLabel, 0, 0);

        _usersPrev = Ui.Outline("← Précédent", 96, 34);
        _usersPrev.Margin = new Padding(0, 6, 6, 0);
        _usersPrev.Click += async (_, _) =>
        {
            if (_usersPage <= 1)
                return;
            _usersPage--;
            await RunAsync(LoadUsersAsync);
        };
        pager.Controls.Add(_usersPrev, 1, 0);

        _usersNext = Ui.Outline("Suivant →", 96, 34);
        _usersNext.Margin = new Padding(0, 6, 0, 0);
        _usersNext.Click += async (_, _) =>
        {
            if (_usersPage >= _usersPages)
                return;
            _usersPage++;
            await RunAsync(LoadUsersAsync);
        };
        pager.Controls.Add(_usersNext, 2, 0);
        page.Controls.Add(pager, 0, 2);

        return page;
    }

    private async Task LoadUsersAsync()
    {
        var status = _usersStatus.SelectedIndex switch
        {
            1 => "Active",
            2 => "Suspended",
            3 => "Disabled",
            _ => null,
        };

        var role = _usersRole.SelectedIndex switch
        {
            1 => "User",
            2 => "DepartmentManager",
            3 => "Supervisor",
            4 => "OrgAdmin",
            5 => "SuperAdmin",
            _ => null,
        };

        var result = await _api.GetSupervisionUsersAsync(
            _usersSearch.Text.Trim(),
            status,
            role,
            page: _usersPage,
            pageSize: 25);

        _usersPages = Math.Max(1, result.TotalPages);
        _usersView.Items.Clear();
        foreach (var user in result.Items)
        {
            var item = new ListViewItem(user.Username);
            item.SubItems.Add(user.DisplayName);
            item.SubItems.Add(Localization.T(RoleLabel(user.Role)));
            item.SubItems.Add(user.DepartmentName ?? "—");
            item.SubItems.Add(StatusLabel(user.Status));
            item.SubItems.Add(user.IsOnline ? "●" : "○");
            item.SubItems.Add(FormatDateTime(user.LastLoginAt));
            item.ForeColor = user.IsActive ? Palette.Text : Palette.Faint;
            _usersView.Items.Add(item);
        }

        _usersPageLabel.Text = Localization.F(
            "Page {0} / {1} · {2} utilisateur(s)",
            _usersPage = Math.Min(_usersPage, _usersPages),
            _usersPages,
            result.TotalCount);
        _usersPrev.Enabled = _usersPage > 1;
        _usersNext.Enabled = _usersPage < _usersPages;
    }

    // ------------------------------------------------------------------ Activité

    private Control BuildActivityPage()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Palette.Background,
        };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 58f));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 42f));

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 4, 0, 4),
        };
        _activityRange = StyledCombo(150);
        _activityRange.Items.AddRange(new object[] { "7 derniers jours", "14 derniers jours", "30 derniers jours", "90 derniers jours" });
        _activityRange.SelectedIndex = 1;
        _activityRange.SelectedIndexChanged += async (_, _) => await RunAsync(LoadActivityAsync);
        bar.Controls.Add(_activityRange);

        _activityConnections = Ui.Label(string.Empty, Palette.Small, Palette.Muted, autoSize: false);
        _activityConnections.Size = new Size(360, 34);
        _activityConnections.Margin = new Padding(8, 8, 0, 0);
        bar.Controls.Add(_activityConnections);
        page.Controls.Add(bar, 0, 0);

        var charts = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Palette.Background,
        };
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

        _activityMessages = new BarChartPanel { BarColor = Palette.Accent };
        charts.Controls.Add(ChartCard("Messages par jour", _activityMessages), 0, 0);

        _activityLogins = new BarChartPanel { BarColor = Palette.Success };
        charts.Controls.Add(ChartCard("Connexions par jour", _activityLogins), 1, 0);
        page.Controls.Add(charts, 0, 1);

        _activityDetail = new ThemedListView();
        SetColumns(_activityDetail,
            ("Date", 150), ("Connexions", 110), ("Échecs", 90),
            ("Messages", 100), ("Utilisateurs actifs", 140), ("Nouveaux comptes", 130));
        page.Controls.Add(ListCard("Détail quotidien", _activityDetail), 0, 2);
        return page;
    }

    private async Task LoadActivityAsync()
    {
        var days = _activityRange.SelectedIndex switch
        {
            0 => 7,
            2 => 30,
            3 => 90,
            _ => 14,
        };

        var activityTask = _api.GetSupervisionActivityAsync(days);
        var connectionsTask = _api.GetSupervisionConnectionsAsync(24);
        await Task.WhenAll(activityTask, connectionsTask);

        var points = await activityTask;
        _activityMessages.SetPoints(points.Select(p => new ChartPoint(p.Date.ToString("dd/MM"), p.Messages)));
        _activityLogins.SetPoints(points.Select(p => new ChartPoint(p.Date.ToString("dd/MM"), p.Logins)));

        var connections = await connectionsTask;
        _activityConnections.Text = Localization.F(
            "Connexions simultanées : {0} · utilisateurs en ligne : {1}",
            connections.CurrentConcurrentConnections,
            connections.CurrentOnlineUsers);

        _activityDetail.Items.Clear();
        foreach (var point in points)
        {
            var item = new ListViewItem(point.Date.ToString("dd/MM/yyyy"));
            item.SubItems.Add(point.Logins.ToString());
            item.SubItems.Add(point.FailedLogins.ToString());
            item.SubItems.Add(point.Messages.ToString());
            item.SubItems.Add(point.ActiveUsers.ToString());
            item.SubItems.Add(point.NewUsers.ToString());
            _activityDetail.Items.Add(item);
        }
    }

    // --------------------------------------------------------------- Départements

    private Control BuildDepartmentsPage()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            BackColor = Palette.Background,
        };

        _departmentsView = new ThemedListView();
        SetColumns(_departmentsView,
            ("Département", 210),
            ("Responsable", 180),
            ("Membres", 90),
            ("Actifs", 80),
            ("En ligne", 90),
            ("Messages (7 j)", 120),
            ("État", 100));
        page.Controls.Add(ListCard("Départements de l'organisation", _departmentsView), 0, 0);
        return page;
    }

    private async Task LoadDepartmentsAsync()
    {
        var departments = await _api.GetSupervisionDepartmentsAsync();
        _departmentsView.Items.Clear();
        foreach (var department in departments)
        {
            var item = new ListViewItem(department.Name);
            item.SubItems.Add(department.ManagerName ?? "—");
            item.SubItems.Add(department.MemberCount.ToString());
            item.SubItems.Add(department.ActiveUsers.ToString());
            item.SubItems.Add(department.OnlineUsers.ToString());
            item.SubItems.Add(department.MessagesLast7Days.ToString());
            item.SubItems.Add(department.IsActive ? Localization.T("Actif") : Localization.T("Désactivé"));
            item.ForeColor = department.IsActive ? Palette.Text : Palette.Faint;
            _departmentsView.Items.Add(item);
        }
    }

    // ------------------------------------------------------------------ Rapports

    private Control BuildReportsPage()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Palette.Background,
        };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 4, 0, 4),
        };
        _reportPeriod = StyledCombo(170);
        _reportPeriod.Items.AddRange(new object[] { "Rapport quotidien", "Rapport hebdomadaire", "Rapport mensuel" });
        _reportPeriod.SelectedIndex = 1;
        _reportPeriod.SelectedIndexChanged += async (_, _) => await RunAsync(LoadReportAsync);
        bar.Controls.Add(_reportPeriod);

        _reportRange = Ui.Label(string.Empty, Palette.Small, Palette.Muted, autoSize: false);
        _reportRange.Size = new Size(420, 34);
        _reportRange.Margin = new Padding(8, 8, 0, 0);
        bar.Controls.Add(_reportRange);
        page.Controls.Add(bar, 0, 0);

        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = Palette.Background,
        };
        for (var i = 0; i < 6; i++)
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 6f));

        var newUsers = MetricCard("Nouveaux comptes", "—", "Sur la période", Palette.Accent, out _reportUsers);
        newUsers.Margin = new Padding(0, 0, 8, 8);
        cards.Controls.Add(newUsers, 0, 0);

        var active = MetricCard("Utilisateurs actifs", "—", "Ayant une activité", Palette.Success, out _reportActive);
        active.Margin = new Padding(0, 0, 8, 8);
        cards.Controls.Add(active, 1, 0);

        var logins = MetricCard("Connexions", "—", "Authentifications", Palette.Warning, out _reportLogins);
        logins.Margin = new Padding(0, 0, 8, 8);
        cards.Controls.Add(logins, 2, 0);

        var messages = MetricCard("Messages", "—", "Envoyés", Palette.Faint, out _reportMessages);
        messages.Margin = new Padding(0, 0, 8, 8);
        cards.Controls.Add(messages, 3, 0);

        var conversations = MetricCard("Conversations", "—", "Créées", Palette.Accent, out _reportConversations);
        conversations.Margin = new Padding(0, 0, 8, 8);
        cards.Controls.Add(conversations, 4, 0);

        var incidents = MetricCard("Incidents", "—", "Échecs & refus", Palette.Danger, out _reportIncidents);
        incidents.Margin = new Padding(0, 0, 0, 8);
        cards.Controls.Add(incidents, 5, 0);
        page.Controls.Add(cards, 0, 1);

        var lists = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Palette.Background,
        };
        lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
        lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));

        _reportTop = new ThemedListView();
        SetColumns(_reportTop, ("Utilisateur", 170), ("Activité", 100));
        lists.Controls.Add(ListCard("Utilisateurs les plus actifs", _reportTop), 0, 0);

        _reportDepartments = new ThemedListView();
        SetColumns(_reportDepartments,
            ("Département", 160), ("Membres", 70), ("Actifs", 70), ("Messages", 90));
        lists.Controls.Add(ListCard("Départements", _reportDepartments), 1, 0);

        page.Controls.Add(lists, 0, 2);
        return page;
    }

    private async Task LoadReportAsync()
    {
        var period = _reportPeriod.SelectedIndex switch
        {
            0 => "daily",
            2 => "monthly",
            _ => "weekly",
        };

        var report = await _api.GetSupervisionReportAsync(period);
        _reportUsers.Text = report.NewUsers.ToString();
        _reportActive.Text = report.ActiveUsers.ToString();
        _reportLogins.Text = report.Logins.ToString();
        _reportMessages.Text = report.Messages.ToString();
        _reportConversations.Text = report.ConversationsCreated.ToString();
        _reportIncidents.Text = report.Incidents.ToString();

        _reportRange.Text = Localization.F(
            "Du {0} au {1} · {2} compte(s) suspendu(s)",
            FormatDateTime(report.From),
            FormatDateTime(report.To),
            report.SuspendedAccounts);

        _reportTop.Items.Clear();
        foreach (var user in report.TopUsers)
        {
            var item = new ListViewItem(user.DisplayName);
            item.SubItems.Add(user.ActivityCount.ToString());
            _reportTop.Items.Add(item);
        }

        _reportDepartments.Items.Clear();
        foreach (var department in report.Departments)
        {
            var item = new ListViewItem(department.Name);
            item.SubItems.Add(department.MemberCount.ToString());
            item.SubItems.Add(department.ActiveUsers.ToString());
            item.SubItems.Add(department.MessagesLast7Days.ToString());
            _reportDepartments.Items.Add(item);
        }
    }

    // --------------------------------------------------------------------- Audit

    private Control BuildAuditPage()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Palette.Background,
        };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 4, 0, 4),
        };

        _auditSearch = StyledSearch("Rechercher une action, un acteur…", 260, out var searchField);
        _auditSearch.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            _auditPage = 1;
            await RunAsync(LoadAuditAsync);
        };
        filters.Controls.Add(searchField);

        _auditAction = StyledCombo(190);
        _auditAction.Items.Add("Toutes les actions");
        _auditAction.SelectedIndex = 0;
        filters.Controls.Add(_auditAction);

        _auditResult = StyledCombo(150);
        _auditResult.Items.Add("Tous les résultats");
        _auditResult.SelectedIndex = 0;
        filters.Controls.Add(_auditResult);

        var searchButton = Ui.Primary("Filtrer", 100, 40);
        searchButton.Click += async (_, _) =>
        {
            _auditPage = 1;
            await RunAsync(LoadAuditAsync);
        };
        filters.Controls.Add(searchButton);
        page.Controls.Add(filters, 0, 0);

        _auditView = new ThemedListView();
        SetColumns(_auditView,
            ("Date", 130),
            ("Acteur", 140),
            ("Action", 160),
            ("Entité", 120),
            ("Résultat", 90),
            ("Détails", 250),
            ("IP", 110));
        page.Controls.Add(ListCard("Journal d'audit (append-only)", _auditView), 0, 1);

        var pager = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Palette.Background,
        };
        pager.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        pager.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        pager.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));

        _auditPageLabel = Ui.Label(string.Empty, Palette.Small, Palette.Muted, autoSize: false);
        _auditPageLabel.Dock = DockStyle.Fill;
        _auditPageLabel.TextAlign = ContentAlignment.MiddleLeft;
        pager.Controls.Add(_auditPageLabel, 0, 0);

        _auditPrev = Ui.Outline("← Précédent", 96, 34);
        _auditPrev.Margin = new Padding(0, 6, 6, 0);
        _auditPrev.Click += async (_, _) =>
        {
            if (_auditPage <= 1)
                return;
            _auditPage--;
            await RunAsync(LoadAuditAsync);
        };
        pager.Controls.Add(_auditPrev, 1, 0);

        _auditNext = Ui.Outline("Suivant →", 96, 34);
        _auditNext.Margin = new Padding(0, 6, 0, 0);
        _auditNext.Click += async (_, _) =>
        {
            if (_auditPage >= _auditPages)
                return;
            _auditPage++;
            await RunAsync(LoadAuditAsync);
        };
        pager.Controls.Add(_auditNext, 2, 0);
        page.Controls.Add(pager, 0, 2);
        return page;
    }

    private async Task LoadAuditAsync()
    {
        if (!_auditFacetsLoaded)
        {
            var facets = await _api.GetAuditFacetsAsync();
            foreach (var facetAction in facets.Actions)
                _auditAction.Items.Add(facetAction);
            foreach (var facetResult in facets.Results)
                _auditResult.Items.Add(facetResult);
            _auditFacetsLoaded = true;
        }

        var action = _auditAction.SelectedIndex > 0 ? _auditAction.SelectedItem?.ToString() : null;
        var result = _auditResult.SelectedIndex > 0 ? _auditResult.SelectedItem?.ToString() : null;

        var page = await _api.GetAuditLogsAsync(
            _auditSearch.Text.Trim(),
            action,
            result: result,
            page: _auditPage,
            pageSize: 25);

        _auditPages = Math.Max(1, page.TotalPages);
        _auditView.Items.Clear();
        foreach (var log in page.Items)
        {
            var item = new ListViewItem(FormatDateTime(log.CreatedAt));
            item.SubItems.Add(log.ActorName);
            item.SubItems.Add(Localization.T(log.Action));
            item.SubItems.Add(string.IsNullOrWhiteSpace(log.EntityType)
                ? "—"
                : $"{log.EntityType} #{log.EntityId}");
            item.SubItems.Add(Localization.T(log.Result));
            item.SubItems.Add(log.Details ?? "—");
            item.SubItems.Add(log.IpAddress ?? "—");
            if (!string.Equals(log.Result, "Success", StringComparison.OrdinalIgnoreCase))
                item.ForeColor = Palette.Warning;
            _auditView.Items.Add(item);
        }

        _auditPageLabel.Text = Localization.F(
            "Page {0} / {1} · {2} entrée(s)",
            _auditPage = Math.Min(_auditPage, _auditPages),
            _auditPages,
            page.TotalCount);
        _auditPrev.Enabled = _auditPage > 1;
        _auditNext.Enabled = _auditPage < _auditPages;
    }

    // --------------------------------------------------------------------- Santé

    private Control BuildHealthPage()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Palette.Background,
        };
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var cards = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Palette.Background,
        };
        for (var i = 0; i < 4; i++)
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        var api = MetricCard("API REST", "—", "Disponibilité", Palette.Success, out _healthApi);
        api.Margin = new Padding(0, 0, 12, 8);
        cards.Controls.Add(api, 0, 0);

        var database = MetricCard("Base de données", "—", "SQLite / EF Core", Palette.Accent, out _healthDatabase);
        database.Margin = new Padding(0, 0, 12, 8);
        cards.Controls.Add(database, 1, 0);

        var signalR = MetricCard("SignalR", "—", "Temps réel", Palette.Warning, out _healthSignalR);
        signalR.Margin = new Padding(0, 0, 12, 8);
        cards.Controls.Add(signalR, 2, 0);

        var sessions = MetricCard("Sessions actives", "—", "Jetons non révoqués", Palette.Faint, out _healthSessions);
        sessions.Margin = new Padding(0, 0, 0, 8);
        cards.Controls.Add(sessions, 3, 0);
        page.Controls.Add(cards, 0, 0);

        var detailsCard = Card();
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(18, 14, 18, 14),
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        var title = Ui.Label("Détails du service", Palette.BodyBold, Palette.Text, autoSize: false);
        title.Dock = DockStyle.Fill;
        layout.Controls.Add(title, 0, 0);

        _healthDetails = Ui.Label(string.Empty, Palette.Body, Palette.Muted, autoSize: false);
        _healthDetails.Dock = DockStyle.Fill;
        layout.Controls.Add(_healthDetails, 0, 1);
        detailsCard.Controls.Add(layout);
        page.Controls.Add(detailsCard, 0, 1);
        return page;
    }

    private async Task LoadHealthAsync()
    {
        var health = await _api.GetSupervisionHealthAsync();
        _healthApi.Text = Localization.T(health.Api);
        _healthDatabase.Text = Localization.T(health.Database);
        _healthSignalR.Text = Localization.T(health.SignalR);
        _healthSessions.Text = health.ActiveSessions.ToString();

        var uptime = TimeSpan.FromSeconds(Math.Max(0, health.UptimeSeconds));
        _healthDetails.Text = string.Join(Environment.NewLine,
            $"{Localization.T("État global")} : {Localization.T(health.Status)}",
            $"{Localization.T("Version")} : {health.Version}",
            $"{Localization.T("Fournisseur de base")} : {health.DatabaseProvider}",
            $"{Localization.T("Disponibilité")} : {(int)uptime.TotalDays} j {uptime.Hours} h {uptime.Minutes} min",
            $"{Localization.T("Heure serveur (UTC)")} : {health.ServerTimeUtc:dd/MM/yyyy HH:mm:ss}",
            $"{Localization.T("Utilisateurs en ligne")} : {health.OnlineUsers}",
            $"{Localization.T("Connexions SignalR simultanées")} : {health.ConcurrentConnections}",
            $"{Localization.T("Sessions actives")} : {health.ActiveSessions}");
    }

    // -------------------------------------------------------------------- Aide

    private static string FormatDateTime(DateTime? value)
    {
        if (value is null)
            return "—";

        var local = value.Value.Kind == DateTimeKind.Utc ? value.Value.ToLocalTime() : value.Value;
        return local.ToString("dd/MM/yyyy HH:mm");
    }

    private static string StatusLabel(string status) => status switch
    {
        "Suspended" => Localization.T("Suspendu"),
        "Disabled" => Localization.T("Désactivé"),
        _ => Localization.T("Actif"),
    };

    private static string RoleLabel(string roleKey) => roleKey switch
    {
        "SuperAdmin" => "Super administrateur",
        "OrgAdmin" => "Administrateur d'organisation",
        "Supervisor" => "Superviseur",
        "DepartmentManager" => "Responsable de département",
        _ => "Utilisateur",
    };
}
