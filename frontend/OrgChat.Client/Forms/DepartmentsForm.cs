using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>
/// Gestion professionnelle des départements : liste maître-détail, création,
/// modification, activation/désactivation, suppression, affectation des
/// membres et statistiques agrégées. Les rôles de lecture seule (Superviseur)
/// consultent la même vue sans les actions d'administration.
/// </summary>
public sealed class DepartmentsForm : Form
{
    private readonly ApiClient _api;
    private readonly AuthResponse _auth;
    private readonly bool _canEdit;

    private readonly List<DepartmentDto> _departments = new();
    private List<UserDto> _users = new();

    private readonly ListView _list = new ThemedListView();
    private readonly ListView _members = new ThemedListView();

    private static readonly Font EmptyIconFont = new("Segoe UI Symbol", 24f);
    private static readonly Font EmptySearchFont = new("Segoe MDL2 Assets", 22f);

    private TextBox _search = null!;
    private Panel _empty = null!;
    private Label _emptyIcon = null!;
    private Label _emptyTitle = null!;
    private Label _emptySub = null!;
    private RoundedButton? _emptyAction;
    private Label _count = null!;
    private Label _name = null!;
    private Label _description = null!;
    private Label _manager = null!;
    private Label _created = null!;
    private Label _status = null!;
    private Label _statMembers = null!;
    private Label _statActive = null!;
    private Label _statOnline = null!;
    private Label _statMessages = null!;
    private Label _membersTitle = null!;
    private Label _error = null!;
    private Label _loading = null!;

    private ComboBox _memberPicker = null!;
    private RoundedButton _addMember = null!;
    private RoundedButton _removeMember = null!;
    private RoundedButton _edit = null!;
    private RoundedButton _toggle = null!;
    private RoundedButton _delete = null!;

    private DepartmentDetailDto? _detail;
    private bool _busy;
    private bool _suppressSelection;
    private int _detailVersion;

    public DepartmentsForm(ApiClient api, AuthResponse auth)
    {
        _api = api;
        _auth = auth;
        _canEdit = auth.Role is "SuperAdmin" or "OrgAdmin";

        Ui.StyleForm(this);
        Text = "OrgChat · Départements";
        ClientSize = new Size(1180, 760);
        MinimumSize = new Size(1080, 700);
        DoubleBuffered = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            BackColor = Palette.Background,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildBody(), 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        Controls.Add(root);

        Localization.Apply(this);
        _ = RefreshAsync(null);
    }

    // ------------------------------------------------------------------ Coquille

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };

        var title = Ui.Label("Départements", Palette.Heading, Palette.Text);
        title.Location = new Point(24, 16);
        header.Controls.Add(title);

        var subtitle = Ui.Label("Structure, responsables et affectations de votre organisation",
            Palette.Small, Palette.Muted);
        subtitle.Location = new Point(26, 52);
        header.Controls.Add(subtitle);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 540,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 22, 24, 0),
        };

        var close = Ui.Outline("Retour au tableau de bord", 190, 40);
        close.Click += (_, _) => Close();
        actions.Controls.Add(close);

        var refresh = Ui.Outline("Actualiser", 110, 40);
        refresh.Margin = new Padding(0, 0, 10, 0);
        refresh.Click += async (_, _) => await RefreshAsync(_detail?.Department.Id);
        actions.Controls.Add(refresh);

        if (_canEdit)
        {
            var create = Ui.Primary("Nouveau département", 180, 40);
            create.Margin = new Padding(0, 0, 10, 0);
            create.Click += OnCreateClicked;
            actions.Controls.Add(create);
        }

        header.Controls.Add(actions);
        return header;
    }

    private Control BuildBody()
    {
        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(24, 0, 24, 14),
            BackColor = Palette.Background,
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        body.Controls.Add(BuildListCard(), 0, 0);
        body.Controls.Add(BuildDetailCard(), 1, 0);
        return body;
    }

    private Control BuildFooter()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(28, 0, 28, 10),
            BackColor = Palette.Background,
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _error = Ui.Label(string.Empty, Palette.Small, Palette.Danger, autoSize: false);
        _error.Dock = DockStyle.Fill;
        footer.Controls.Add(_error, 0, 0);

        _loading = Ui.Label(string.Empty, Palette.Small, Palette.Muted, autoSize: false);
        _loading.AutoSize = true;
        _loading.Anchor = AnchorStyles.Right;
        footer.Controls.Add(_loading, 1, 0);

        return footer;
    }

    private Control BuildListCard()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            Margin = new Padding(0, 0, 16, 0),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14),
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        var search = Ui.SearchField("Rechercher un département…", out _search, height: 38);
        search.Dock = DockStyle.Fill;
        _search.TextChanged += OnSearchChanged;
        layout.Controls.Add(search, 0, 0);

        _list.Dock = DockStyle.Fill;
        _list.Columns.Add("Département", 224);
        _list.Columns.Add("Membres", 80);
        _list.SelectedIndexChanged += OnSelectionChanged;
        _list.DoubleClick += (_, _) => OnEditClicked(this, EventArgs.Empty);
        layout.Controls.Add(_list, 0, 1);

        _empty = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Palette.Surface,
            Visible = false,
            Margin = Padding.Empty,
        };
        _emptyIcon = Ui.Label("▭", EmptyIconFont, Palette.Faint, autoSize: false);
        _emptyIcon.TextAlign = ContentAlignment.MiddleCenter;
        _emptyTitle = Ui.Label("Aucun département", Palette.BodyBold, Palette.Text, autoSize: false);
        _emptyTitle.TextAlign = ContentAlignment.MiddleCenter;
        _emptySub = Ui.Label("Créez votre premier département pour organiser vos équipes.",
            Palette.Small, Palette.Muted, autoSize: false);
        _emptySub.TextAlign = ContentAlignment.MiddleCenter;
        _empty.Controls.Add(_emptyIcon);
        _empty.Controls.Add(_emptyTitle);
        _empty.Controls.Add(_emptySub);

        if (_canEdit)
        {
            var emptyCreate = Ui.Primary("Créer un département", 190, 36);
            _emptyAction = emptyCreate;
            emptyCreate.Click += (_, _) =>
            {
                if (_search.TextLength > 0)
                {
                    _search.Clear();
                    _search.Focus();
                    return;
                }

                OnCreateClicked(this, EventArgs.Empty);
            };
            _empty.Controls.Add(emptyCreate);
            void LayoutEmpty()
            {
                var contentHeight = 34 + 6 + 22 + 4 + 18 + 10 + 36;
                var top = Math.Max(4, (_empty.ClientSize.Height - contentHeight) / 2);
                _emptyIcon.SetBounds(0, top, Math.Max(100, _empty.ClientSize.Width), 34);
                _emptyTitle.SetBounds(8, top + 40, Math.Max(100, _empty.ClientSize.Width - 16), 22);
                _emptySub.SetBounds(8, top + 66, Math.Max(100, _empty.ClientSize.Width - 16), 18);
                emptyCreate.Left = Math.Max(0, (_empty.ClientSize.Width - emptyCreate.Width) / 2);
                emptyCreate.Top = top + 94;
            }
            _empty.Resize += (_, _) => LayoutEmpty();
            LayoutEmpty();
        }
        else
        {
            void LayoutEmpty()
            {
                var contentHeight = 34 + 6 + 22 + 4 + 18;
                var top = Math.Max(4, (_empty.ClientSize.Height - contentHeight) / 2);
                _emptyIcon.SetBounds(0, top, Math.Max(100, _empty.ClientSize.Width), 34);
                _emptyTitle.SetBounds(8, top + 40, Math.Max(100, _empty.ClientSize.Width - 16), 22);
                _emptySub.SetBounds(8, top + 66, Math.Max(100, _empty.ClientSize.Width - 16), 18);
            }
            _empty.Resize += (_, _) => LayoutEmpty();
            LayoutEmpty();
        }

        layout.Controls.Add(_empty, 0, 1);
        _empty.BringToFront();

        _count = Ui.Label("0 département", Palette.Small, Palette.Muted, autoSize: false);
        _count.Dock = DockStyle.Fill;
        _count.TextAlign = ContentAlignment.BottomLeft;
        layout.Controls.Add(_count, 0, 2);

        card.Controls.Add(layout);
        return card;
    }

    private Control BuildDetailCard()
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(20, 16, 20, 16),
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var titleBlock = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        titleBlock.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        titleBlock.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _name = Ui.Label("Sélectionnez un département", Palette.Title, Palette.Text, autoSize: false);
        _name.Dock = DockStyle.Fill;
        titleBlock.Controls.Add(_name, 0, 0);

        _description = Ui.Label(string.Empty, Palette.Small, Palette.Muted, autoSize: false);
        _description.Dock = DockStyle.Fill;
        titleBlock.Controls.Add(_description, 0, 1);
        layout.Controls.Add(titleBlock, 0, 0);

        var info = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
        };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
        info.Controls.Add(InfoBlock("Responsable", out _manager), 0, 0);
        info.Controls.Add(InfoBlock("Créé le", out _created), 1, 0);
        info.Controls.Add(InfoBlock("État", out _status), 2, 0);
        layout.Controls.Add(info, 0, 1);

        var stats = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 0, 4),
        };
        for (var i = 0; i < 4; i++)
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        var membersCard = MetricCard("Membres", "Affectés", Palette.Accent, out _statMembers);
        membersCard.Margin = new Padding(0, 0, 10, 0);
        stats.Controls.Add(membersCard, 0, 0);

        var activeCard = MetricCard("Actifs", "Comptes actifs", Palette.Success, out _statActive);
        activeCard.Margin = new Padding(0, 0, 10, 0);
        stats.Controls.Add(activeCard, 1, 0);

        var onlineCard = MetricCard("En ligne", "Présence temps réel", Palette.Warning, out _statOnline);
        onlineCard.Margin = new Padding(0, 0, 10, 0);
        stats.Controls.Add(onlineCard, 2, 0);

        var messagesCard = MetricCard("Messages", "Envoyés", Palette.Faint, out _statMessages);
        messagesCard.Margin = new Padding(0);
        stats.Controls.Add(messagesCard, 3, 0);

        layout.Controls.Add(stats, 0, 2);

        var memberBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
        };
        memberBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        memberBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _membersTitle = Ui.Label("Membres", Palette.BodyBold, Palette.Text, autoSize: false);
        _membersTitle.Dock = DockStyle.Fill;
        _membersTitle.TextAlign = ContentAlignment.MiddleLeft;
        memberBar.Controls.Add(_membersTitle, 0, 0);

        var memberActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            AutoSize = true,
        };

        _removeMember = Ui.Outline("Retirer", 86, 34);
        _removeMember.Margin = new Padding(8, 4, 0, 0);
        _removeMember.Enabled = false;
        _removeMember.Click += OnRemoveMemberClicked;
        memberActions.Controls.Add(_removeMember);

        _addMember = Ui.Primary("Ajouter", 86, 34);
        _addMember.Margin = new Padding(8, 4, 0, 0);
        _addMember.Click += OnAddMemberClicked;
        memberActions.Controls.Add(_addMember);

        _memberPicker = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Palette.SurfaceAlt,
            ForeColor = Palette.Text,
            Font = Palette.Body,
            Width = 210,
            Margin = new Padding(8, 4, 0, 0),
            DisplayMember = nameof(UserDto.DisplayName),
        };
        memberActions.Controls.Add(_memberPicker);

        memberBar.Controls.Add(memberActions, 1, 0);
        layout.Controls.Add(memberBar, 0, 3);

        _members.Dock = DockStyle.Fill;
        _members.Columns.Add("Nom", 220);
        _members.Columns.Add("Identifiant", 140);
        _members.Columns.Add("Principal", 80);
        _members.Columns.Add("Depuis", 100);
        _members.SelectedIndexChanged += (_, _) =>
            _removeMember.Enabled = CanManageMembers() && _members.SelectedItems.Count > 0;

        var membersHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        _members.Dock = DockStyle.Fill;
        membersHost.Controls.Add(_members);
        layout.Controls.Add(membersHost, 0, 4);

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 8, 0, 0),
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var hint = Ui.Label(_canEdit
                ? "Gérez les départements, leurs responsables et leurs membres."
                : "Consultation en lecture seule : les actions d'administration sont réservées aux administrateurs.",
            Palette.Small, Palette.Faint, autoSize: false);
        hint.Dock = DockStyle.Fill;
        hint.TextAlign = ContentAlignment.MiddleLeft;
        actions.Controls.Add(hint, 0, 0);

        var actionButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            AutoSize = true,
        };

        _delete = Ui.Danger("Supprimer", 100, 38);
        _delete.Margin = new Padding(8, 0, 0, 0);
        _delete.Click += OnDeleteClicked;
        actionButtons.Controls.Add(_delete);

        _toggle = Ui.Outline("Désactiver", 110, 38);
        _toggle.Margin = new Padding(8, 0, 0, 0);
        _toggle.Click += OnToggleClicked;
        actionButtons.Controls.Add(_toggle);

        _edit = Ui.Primary("Modifier", 110, 38);
        _edit.Margin = new Padding(8, 0, 0, 0);
        _edit.Click += OnEditClicked;
        actionButtons.Controls.Add(_edit);

        actions.Controls.Add(actionButtons, 1, 0);
        layout.Controls.Add(actions, 0, 5);

        card.Controls.Add(layout);
        return card;
    }

    private static Control InfoBlock(string caption, out Label value)
    {
        var block = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        block.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        block.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var label = Ui.Label(caption, Palette.Small, Palette.Muted, autoSize: false);
        label.Dock = DockStyle.Fill;
        block.Controls.Add(label, 0, 0);

        value = Ui.Label("—", Palette.BodyBold, Palette.Text, autoSize: false);
        value.Dock = DockStyle.Fill;
        block.Controls.Add(value, 0, 1);
        return block;
    }

    private static RoundedPanel MetricCard(string caption, string hint, Color accent, out Label valueLabel)
    {
        var card = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 10,
            FillColor = Palette.SurfaceAlt,
            BorderColor = Palette.Border,
            ShowBorder = false,
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12, 7, 12, 7),
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 17));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var captionLabel = Ui.Label(caption, Palette.Small, Palette.Muted, autoSize: false);
        captionLabel.Dock = DockStyle.Fill;
        layout.Controls.Add(captionLabel, 0, 0);

        valueLabel = Ui.Label("—", Palette.Title, accent, autoSize: false);
        valueLabel.Dock = DockStyle.Fill;
        layout.Controls.Add(valueLabel, 0, 1);

        var hintLabel = Ui.Label(hint, Palette.Small, Palette.Faint, autoSize: false);
        hintLabel.Dock = DockStyle.Fill;
        layout.Controls.Add(hintLabel, 0, 2);

        card.Controls.Add(layout);
        return card;
    }

    // ------------------------------------------------------------- Chargement

    private async void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressSelection || _busy)
            return;

        var department = SelectedDepartment();
        if (department is null)
        {
            ClearDetail();
            return;
        }

        await LoadDetailAsync(department);
    }

    private async void OnSearchChanged(object? sender, EventArgs e)
    {
        if (_busy)
            return;

        var previousId = _detail?.Department.Id;
        PopulateList(previousId);

        var selected = SelectedDepartment();
        if (selected is null)
        {
            ClearDetail();
            return;
        }

        if (selected.Id != previousId)
            await LoadDetailAsync(selected);
    }

    private async Task RefreshAsync(int? selectId, bool clearFilter = false)
    {
        if (_busy)
            return;

        SetBusy(true);
        try
        {
            if (clearFilter)
                _search.Text = string.Empty;

            var departmentsTask = _api.GetDepartmentsAsync();
            var usersTask = _api.GetUsersAsync();
            await Task.WhenAll(departmentsTask, usersTask);

            _departments.Clear();
            _departments.AddRange(departmentsTask.Result
                .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase));
            _users = usersTask.Result
                .OrderBy(u => u.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            PopulateList(selectId);
            _error.Text = string.Empty;

            var selected = SelectedDepartment();
            if (selected is null)
            {
                ClearDetail();
                return;
            }

            await LoadDetailCoreAsync(selected);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task LoadDetailAsync(DepartmentDto department)
    {
        var version = ++_detailVersion;
        SetBusy(true);
        try
        {
            await LoadDetailCoreAsync(department, version);
            if (version == _detailVersion)
                _error.Text = string.Empty;
        }
        catch (Exception ex)
        {
            if (version == _detailVersion)
                ShowError(ex);
        }
        finally
        {
            if (version == _detailVersion)
                SetBusy(false);
        }
    }

    private async Task LoadDetailCoreAsync(DepartmentDto department, int? version = null)
    {
        var detailTask = _api.GetDepartmentAsync(department.Id);
        var statsTask = _api.GetDepartmentStatsAsync(department.Id);
        await Task.WhenAll(detailTask, statsTask);

        if (version.HasValue && version.Value != _detailVersion)
            return;

        _detail = detailTask.Result;
        RenderDetail(_detail, statsTask.Result);
    }

    private void PopulateList(int? selectId)
    {
        var filter = _search.Text.Trim();
        var visible = _departments
            .Where(d => filter.Length == 0 ||
                        d.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ||
                        (d.ManagerName?.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ?? false))
            .ToList();

        _suppressSelection = true;
        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            foreach (var department in visible)
            {
                var item = new ListViewItem(department.Name) { Tag = department };
                item.SubItems.Add(department.MemberCount.ToString());
                if (!department.IsActive)
                    item.ForeColor = Palette.Muted;
                _list.Items.Add(item);
            }

            if (visible.Count > 0)
            {
                var target = visible.FirstOrDefault(d => d.Id == selectId) ?? visible[0];
                var targetItem = _list.Items.Cast<ListViewItem>()
                    .FirstOrDefault(i => ((DepartmentDto)i.Tag!).Id == target.Id);
                if (targetItem is not null)
                {
                    targetItem.Selected = true;
                    targetItem.Focused = true;
                    _list.EnsureVisible(targetItem.Index);
                }
            }
        }
        finally
        {
            _list.EndUpdate();
            _suppressSelection = false;
        }

        var empty = visible.Count == 0;
        var noResult = empty && filter.Length > 0 && _departments.Count > 0;
        _count.Text = noResult
            ? Localization.T("Aucun résultat")
            : visible.Count == 1
                ? Localization.T("1 département")
                : Localization.F("{0} départements", visible.Count);

        _list.Visible = !empty;
        _empty.Visible = empty;
        if (empty)
        {
            UpdateEmptyState(noResult);
            _empty.BringToFront();
        }
    }

    private void UpdateEmptyState(bool noResult)
    {
        _emptyIcon.Text = noResult ? "\uE721" : "▭";
        _emptyIcon.Font = noResult ? EmptySearchFont : EmptyIconFont;
        _emptyTitle.Text = Localization.T(noResult ? "Aucun résultat" : "Aucun département");
        _emptySub.Text = Localization.T(noResult
            ? "Aucun département ne correspond à votre recherche."
            : "Créez votre premier département pour organiser vos équipes.");

        if (_emptyAction is not null)
        {
            _emptyAction.Text = Localization.T(noResult
                ? "Effacer la recherche"
                : "Créer un département");
        }
    }

    private void RenderDetail(DepartmentDetailDto detail, DepartmentStatsDto stats)
    {
        var department = detail.Department;

        _name.Text = department.Name;
        _description.Text = string.IsNullOrWhiteSpace(department.Description)
            ? Localization.T("Aucune description renseignée.")
            : department.Description;
        _manager.Text = department.ManagerName ?? Localization.T("Non désigné");
        _created.Text = department.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy");
        _status.Text = department.IsActive
            ? Localization.T("Actif")
            : Localization.T("Désactivé");
        _status.ForeColor = department.IsActive ? Palette.Success : Palette.Danger;

        _statMembers.Text = stats.MemberCount.ToString();
        _statActive.Text = stats.ActiveUsers.ToString();
        _statOnline.Text = stats.OnlineUsers.ToString();
        _statMessages.Text = stats.MessageCount.ToString();

        _members.BeginUpdate();
        try
        {
            _members.Items.Clear();
            foreach (var member in detail.Members)
            {
                var item = new ListViewItem(member.DisplayName) { Tag = member };
                item.SubItems.Add(member.Username);
                item.SubItems.Add(member.IsPrimary ? Localization.T("Oui") : "—");
                item.SubItems.Add(member.JoinedAt.ToLocalTime().ToString("dd/MM/yyyy"));
                _members.Items.Add(item);
            }
        }
        finally
        {
            _members.EndUpdate();
        }

        _membersTitle.Text = Localization.F("Membres ({0})", detail.Members.Count);

        var memberIds = detail.Members.Select(m => m.UserId).ToHashSet();
        _memberPicker.Items.Clear();
        foreach (var user in _users.Where(u => !memberIds.Contains(u.Id)))
            _memberPicker.Items.Add(user);
        if (_memberPicker.Items.Count > 0)
            _memberPicker.SelectedIndex = 0;

        var canManage = CanManageMembers();
        _memberPicker.Visible = canManage;
        _addMember.Visible = canManage;
        _removeMember.Visible = canManage;
        _removeMember.Enabled = canManage && _members.SelectedItems.Count > 0;

        _edit.Visible = _canEdit;
        _toggle.Visible = _canEdit;
        _delete.Visible = _canEdit;
        _toggle.Text = department.IsActive
            ? Localization.T("Désactiver")
            : Localization.T("Réactiver");

        _members.Focus();
    }

    private void ClearDetail()
    {
        _detail = null;
        _detailVersion++;
        _name.Text = Localization.T("Sélectionnez un département");
        _description.Text = Localization.T("Choisissez un département dans la liste ou créez-en un.");
        _manager.Text = "—";
        _created.Text = "—";
        _status.Text = "—";
        _status.ForeColor = Palette.Muted;

        _statMembers.Text = "—";
        _statActive.Text = "—";
        _statOnline.Text = "—";
        _statMessages.Text = "—";

        _members.BeginUpdate();
        try
        {
            _members.Items.Clear();
        }
        finally
        {
            _members.EndUpdate();
        }

        _membersTitle.Text = Localization.T("Membres");
        _memberPicker.Items.Clear();

        _memberPicker.Visible = false;
        _addMember.Visible = false;
        _removeMember.Visible = false;
        _edit.Visible = false;
        _toggle.Visible = false;
        _delete.Visible = false;
    }

    // --------------------------------------------------------------- Actions

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        if (_busy || !_canEdit)
            return;

        using var dialog = new DepartmentEditForm(null, _users);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var created = await _api.CreateDepartmentAsync(new DepartmentCreateRequest
            {
                Name = dialog.DepartmentName,
                Description = dialog.Description,
                ManagerUserId = dialog.ManagerUserId,
            });

            await RefreshAsync(created.Id, clearFilter: true);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_busy || !_canEdit || _detail is null)
            return;

        var current = _detail.Department;
        using var dialog = new DepartmentEditForm(current, _users);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            await _api.UpdateDepartmentAsync(current.Id, new DepartmentUpdateRequest
            {
                Name = dialog.DepartmentName,
                Description = dialog.Description,
                ManagerUserId = dialog.ManagerUserId,
                IsActive = dialog.IsActive,
            });

            await RefreshAsync(current.Id);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void OnToggleClicked(object? sender, EventArgs e)
    {
        if (_busy || !_canEdit || _detail is null)
            return;

        var current = _detail.Department;
        var activate = !current.IsActive;

        try
        {
            await _api.UpdateDepartmentAsync(current.Id, new DepartmentUpdateRequest
            {
                IsActive = activate,
                ManagerUserId = current.ManagerUserId,
            });

            await RefreshAsync(current.Id);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_busy || !_canEdit || _detail is null)
            return;

        var current = _detail.Department;
        var confirmation = MessageBox.Show(this,
            $"Supprimer définitivement le département « {current.Name} » ?\n\n" +
            "Les membres doivent d'abord être retirés.",
            "Supprimer le département",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

        if (confirmation != DialogResult.Yes)
            return;

        try
        {
            await _api.DeleteDepartmentAsync(current.Id);
            await RefreshAsync(null);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void OnAddMemberClicked(object? sender, EventArgs e)
    {
        if (_busy || _detail is null || !CanManageMembers())
            return;

        if (_memberPicker.SelectedItem is not UserDto user)
        {
            MessageBox.Show(this, "Aucun utilisateur disponible à affecter.",
                "Départements", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            await _api.AddDepartmentMemberAsync(_detail.Department.Id, user.Id);
            await RefreshAsync(_detail.Department.Id);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async void OnRemoveMemberClicked(object? sender, EventArgs e)
    {
        if (_busy || _detail is null || !CanManageMembers())
            return;

        if (_members.SelectedItems.Count == 0 ||
            _members.SelectedItems[0].Tag is not DepartmentMemberDto member)
            return;

        var confirmation = MessageBox.Show(this,
            $"Retirer {member.DisplayName} du département « {_detail.Department.Name} » ?",
            "Retirer un membre",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

        if (confirmation != DialogResult.Yes)
            return;

        try
        {
            await _api.RemoveDepartmentMemberAsync(_detail.Department.Id, member.UserId);
            await RefreshAsync(_detail.Department.Id);
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    // ---------------------------------------------------------------- Utilitaires

    private bool CanManageMembers() =>
        _canEdit ||
        (_auth.Role == "DepartmentManager" &&
         _detail is not null &&
         _detail.Department.ManagerUserId == _auth.UserId);

    private DepartmentDto? SelectedDepartment() =>
        _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as DepartmentDto : null;

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _loading.Text = busy ? Localization.T("Synchronisation…") : string.Empty;
        UseWaitCursor = busy;
    }

    private void ShowError(Exception exception)
    {
        _error.Text = Ui.FriendlyError(exception);
    }
}
