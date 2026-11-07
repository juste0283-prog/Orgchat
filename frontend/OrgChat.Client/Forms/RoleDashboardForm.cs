using OrgChat.Client.Configuration;
using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>
/// Tableau de bord professionnel par rôle (SuperAdmin, OrgAdmin,
/// Superviseur, Responsable de département).
/// Coquille moderne : barre latérale de navigation, cartes de statistiques,
/// tableaux réels via les endpoints, états de chargement.
/// Les utilisateurs simples ouvrent directement la messagerie (MainForm).
/// </summary>
public sealed class RoleDashboardForm : Form
{
    private readonly ApiClient _api;
    private readonly AuthResponse _auth;

    private readonly ListView _usersView = new ThemedListView();
    private readonly ListView _departmentsView = new ThemedListView();
    private Label _loadingLabel = null!;
    private Label _errorLabel = null!;
    private TextBox _searchBox = null!;
    private TableLayoutPanel _cardsPanel = null!;
    private List<UserDto> _allUsers = new();
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
        ClientSize = new Size(1020, 700);
        MinimumSize = new Size(1024, 680);
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
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 248));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        root.Controls.Add(BuildSidebar(), 0, 0);
        root.Controls.Add(BuildMain(), 1, 0);

        Controls.Add(root);
        Localization.Apply(this);
        _ = LoadAllAsync();
    }

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
            RowCount = 4,
            Padding = new Padding(16, 18, 16, 16),
            BackColor = Color.Transparent,
        };
        stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var brandHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var mark = new BrandMark { Location = new Point(0, 2) };
        brandHost.Controls.Add(mark);
        var brand = Ui.Label("OrgChat", new Font("Segoe UI", 16f, FontStyle.Bold), Palette.Text, autoSize: false);
        brand.Location = new Point(46, 2);
        brand.Size = new Size(180, 34);
        brandHost.Controls.Add(brand);
        var subBrand = Ui.Label("Plateforme", Palette.Small, Palette.Muted, autoSize: false);
        subBrand.Location = new Point(46, 36);
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

        var isAdmin = _auth.Role is "SuperAdmin" or "OrgAdmin";
        var canSeeDepartments = isAdmin || _auth.Role is "DepartmentManager" or "Supervisor" or "SuperAdmin" or "OrgAdmin";

        nav.Controls.Add(NavButton("▦  Dashboard", active: true));
        nav.Controls.Add(NavButton("◉  Utilisateurs", enabled: isAdmin, click: OnManageUsersClicked));
        nav.Controls.Add(NavButton("▭  Départements", enabled: canSeeDepartments, click: OnManageDepartmentsClicked));
        nav.Controls.Add(NavButton("‣  Supervision", enabled: _auth.Role is "SuperAdmin" or "OrgAdmin" or "Supervisor", click: OnSupervisionClicked));
        nav.Controls.Add(NavButton("✉  Messagerie", click: (_, _) => OpenMessagingRequested?.Invoke(this, EventArgs.Empty)));
        stack.Controls.Add(nav, 0, 1);

        var userHost = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            Height = 72,
        };
        var avatar = new AvatarControl
        {
            DisplayName = _auth.DisplayName,
            Seed = _auth.UserId.ToString(),
            Location = new Point(10, 10),
            Size = new Size(48, 48),
        };
        userHost.Controls.Add(avatar);
        var name = Ui.Label(_auth.DisplayName, Palette.BodyBold, Palette.Text, autoSize: false);
        name.Location = new Point(68, 8);
        name.Size = new Size(140, 20);
        userHost.Controls.Add(name);
        var role = Ui.Label(RoleLabel(_auth.Role), Palette.Small, Palette.Muted, autoSize: false);
        role.Location = new Point(68, 30);
        role.Size = new Size(140, 16);
        userHost.Controls.Add(role);

        stack.Controls.Add(userHost, 0, 2);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Color.Transparent,
        };
        var profileButton = Ui.Outline("Mon profil", 208, 38);
        profileButton.Click += OnProfileClicked;
        bottom.Controls.Add(profileButton);
        var settingsButton = Ui.Outline("⚙  Paramètres", 208, 38);
        settingsButton.Click += (_, _) => SettingsChanged?.Invoke(this, EventArgs.Empty);
        bottom.Controls.Add(settingsButton);
        var logoutButton = Ui.Danger("Se déconnecter", 208, 38);
        logoutButton.Click += (_, _) => LogoutRequested?.Invoke(this, EventArgs.Empty);
        bottom.Controls.Add(logoutButton);
        stack.Controls.Add(bottom, 0, 3);

        sidebar.Controls.Add(stack);
        return sidebar;
    }

    private RoundedButton NavButton(string text, bool active = false, bool enabled = true, EventHandler? click = null)
    {
        var button = Ui.Outline(text, 208, 40);
        button.Enabled = enabled;
        if (active)
        {
            button.NormalColor = Palette.AccentSoft;
            button.ForeColor = Palette.Text;
        }
        if (click != null)
            button.Click += click;
        button.Margin = new Padding(0, 0, 0, 6);
        return button;
    }

    private Panel BuildMain()
    {
        var host = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(22),
            BackColor = Palette.Background,
        };
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        host.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));

        // --- En-tête ---
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Palette.Background,
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        var titleHost = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var title = Ui.Label("Tableau de bord", Palette.Heading, Palette.Text, autoSize: false);
        title.Location = new Point(0, 0);
        title.Size = new Size(400, 30);
        titleHost.Controls.Add(title);
        var subtitle = Ui.Label(
            $"{_auth.DisplayName} (@{_auth.Username}) · {RoleLabel(_auth.Role)}",
            Palette.Small,
            Palette.Muted,
            autoSize: false);
        subtitle.Location = new Point(0, 32);
        subtitle.Size = new Size(400, 20);
        titleHost.Controls.Add(subtitle);
        header.Controls.Add(titleHost, 0, 0);

        // Recherche (filtre réel sur la liste des utilisateurs)
        var searchField = Ui.Field("Rechercher un utilisateur…", out var searchBox);
        searchBox.TextChanged += (_, _) => ApplyUserFilter();
        header.Controls.Add(searchField, 1, 0);

        var refresh = Ui.Outline("Actualiser", 100, 40);
        refresh.Click += async (_, _) => await LoadAllAsync();
        header.Controls.Add(refresh, 2, 0);
        host.Controls.Add(header, 0, 0);

        // --- Cartes de statistiques ---
        _cardsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Palette.Background,
        };
        for (var i = 0; i < 4; i++)
            _cardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        host.Controls.Add(_cardsPanel, 0, 1);

        // --- Contenu ---
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Palette.Background,
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));

        var usersPanel = BuildTablePanel("Utilisateurs de l'organisation", _usersView);
        content.Controls.Add(usersPanel, 0, 0);

        var rightColumn = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Palette.Background,
        };
        rightColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 60f));
        rightColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 40f));
        rightColumn.Controls.Add(BuildTablePanel("Départements", _departmentsView), 0, 0);

        var help = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
        };
        var helpStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16),
            BackColor = Color.Transparent,
        };
        helpStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        helpStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        helpStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        var helpTitle = Ui.Label("Actions rapides", Palette.BodyBold, Palette.Text);
        helpStack.Controls.Add(helpTitle, 0, 0);
        var helpText = Ui.Label(
            "Ajoutez un utilisateur depuis « Utilisateurs ».\nLe lien d’activation est généré automatiquement.\nLa supervision n’affiche que des métadonnées.",
            Palette.Small,
            Palette.Muted,
            autoSize: false);
        helpStack.Controls.Add(helpText, 0, 1);
        var openMessaging = Ui.Primary("Ouvrir la messagerie", 200, 40);
        openMessaging.Click += (_, _) => OpenMessagingRequested?.Invoke(this, EventArgs.Empty);
        helpStack.Controls.Add(openMessaging, 0, 2);
        help.Controls.Add(helpStack);
        rightColumn.Controls.Add(help, 0, 1);
        content.Controls.Add(rightColumn, 1, 0);

        host.Controls.Add(content, 0, 2);

        // --- Pied de page ---
        var footer = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        _loadingLabel = Ui.Label(string.Empty, Palette.Small, Palette.Muted, autoSize: false);
        _loadingLabel.Dock = DockStyle.Top;
        footer.Controls.Add(_loadingLabel);
        _errorLabel = Ui.Label(string.Empty, Palette.Small, Palette.Danger, autoSize: false);
        _errorLabel.Dock = DockStyle.Top;
        footer.Controls.Add(_errorLabel);
        host.Controls.Add(footer, 0, 3);

        StyleListView(_usersView, new[]
        {
            ("Utilisateurs", 90),
            ("Nom affiché", 150),
            ("Rôle", 110),
            ("Statut", 70),
            ("En ligne", 60),
        });
        StyleListView(_departmentsView, new[]
        {
            ("Département", 130),
            ("Membres", 65),
            ("Responsable", 110),
        });

        return host;
    }

    private static RoundedPanel BuildTablePanel(string title, ListView listView)
    {
        var panel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
        };
        var headerLabel = Ui.Label(title, Palette.BodyBold, Palette.Text, autoSize: false);
        headerLabel.Location = new Point(16, 10);
        headerLabel.Size = new Size(panel.Width > 0 ? panel.Width - 32 : 300, 22);
        panel.Controls.Add(headerLabel);
        listView.Dock = DockStyle.Fill;
        listView.Margin = new Padding(8, 36, 8, 8);
        listView.Location = new Point(8, 36);
        listView.Size = new Size(300, 200);
        panel.Controls.Add(listView);
        return panel;
    }

    private static void StyleListView(ListView listView, (string Title, int Width)[] columns)
    {
        listView.BackColor = Palette.Surface;
        listView.ForeColor = Palette.Text;
        listView.Font = Palette.Body;
        listView.GridLines = false;
        listView.MultiSelect = false;
        listView.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        foreach (var (title, width) in columns)
            listView.Columns.Add(title, width);
    }

    private async Task LoadAllAsync()
    {
        if (_busy)
            return;
        _busy = true;
        UseWaitCursor = true;
        _loadingLabel.Text = Localization.T("Chargement des statistiques…");
        _errorLabel.Text = string.Empty;

        try
        {
            var users = await _api.GetUsersAsync();
            var departments = await _api.GetDepartmentsAsync();
            var conversations = await _api.GetConversationsAsync();

            _allUsers = users;
            BuildCards(
                ("Utilisateurs", users.Count.ToString(), Palette.Accent),
                ("Actifs", users.Count(u => u.IsActive).ToString(), Palette.Success),
                ("En ligne", users.Count(u => u.IsOnline).ToString(), Palette.Warning),
                ("Conversations", conversations.Count.ToString(), Palette.Faint));

            ApplyUserFilter();
            _departmentsView.Items.Clear();
            foreach (var department in departments.OrderBy(d => d.Name))
            {
                var item = new ListViewItem(department.Name);
                item.SubItems.Add(department.MemberCount.ToString());
                item.SubItems.Add(department.ManagerName ?? "—");
                _departmentsView.Items.Add(item);
            }
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

    private void BuildCards(params (string Title, string Value, Color Accent)[] cards)
    {
        _cardsPanel.Controls.Clear();
        for (var i = 0; i < cards.Length && i < 4; i++)
        {
            var (title, value, accent) = cards[i];
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                CornerRadius = 12,
                FillColor = Palette.Surface,
                BorderColor = Palette.Border,
                Margin = new Padding(0, 0, 12, 0),
            };
            var dot = new Panel
            {
                BackColor = accent,
                Location = new Point(16, 18),
                Size = new Size(10, 10),
            };
            card.Controls.Add(dot);
            var caption = Ui.Label(title.ToUpperInvariant(), Palette.Small, Palette.Muted, autoSize: false);
            caption.Location = new Point(34, 16);
            caption.Size = new Size(140, 16);
            card.Controls.Add(caption);
            var number = Ui.Label(value, new Font("Segoe UI", 24f, FontStyle.Bold), Palette.Text, autoSize: false);
            number.Location = new Point(16, 38);
            number.Size = new Size(140, 40);
            card.Controls.Add(number);
            _cardsPanel.Controls.Add(card, i, 0);
        }
    }

    private void ApplyUserFilter()
    {
        var term = (_searchBox?.Text ?? string.Empty).Trim().ToLowerInvariant();
        var filtered = string.IsNullOrEmpty(term)
            ? _allUsers
            : _allUsers.Where(u =>
                u.Username.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();

        _usersView.Items.Clear();
        foreach (var user in filtered.OrderBy(u => u.DisplayName).Take(100))
        {
            var item = new ListViewItem(user.Username);
            item.SubItems.Add(user.DisplayName);
            item.SubItems.Add(RoleLabel(user.Role));
            item.SubItems.Add(user.IsActive ? Localization.T("actif") : Localization.T("inactif"));
            item.SubItems.Add(user.IsOnline ? "●" : "○");
            _usersView.Items.Add(item);
        }
    }

    private async void OnProfileClicked(object? sender, EventArgs e)
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

    private async void OnManageDepartmentsClicked(object? sender, EventArgs e)
    {
        try
        {
            var departments = await _api.GetDepartmentsAsync();
            var list = departments.Count == 0
                ? "Aucun département."
                : string.Join(Environment.NewLine, departments.Select(d => $"• {d.Name} — {d.MemberCount} membre(s)"));
            MessageBox.Show(this, list, "Départements de votre organisation", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _ = LoadAllAsync();
        }
        catch (Exception ex)
        {
            _errorLabel.Text = Ui.FriendlyError(ex);
        }
    }

    private void OnSupervisionClicked(object? sender, EventArgs e)
    {
        using var form = new SupervisionForm(_api, _auth);
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
}
