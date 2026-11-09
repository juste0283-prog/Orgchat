using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>
/// Création d'un compte par un administrateur. Deux modes explicites :
/// lien d'activation (l'utilisateur choisit son mot de passe) ou mot de
/// passe immédiat. Le lien est ensuite affiché dans une fenêtre dédiée.
/// </summary>
public sealed class CreateUserForm : Form
{
    private readonly ApiClient _api;
    private readonly TextBox _firstNameBox;
    private readonly TextBox _lastNameBox;
    private readonly TextBox _emailBox;
    private readonly TextBox _jobTitleBox;
    private readonly ComboBox _departmentCombo;
    private readonly ComboBox _roleCombo;
    private readonly CheckBox _directPasswordBox;
    private readonly CheckBox _isActiveBox;
    private readonly TextBox _passwordBox;
    private readonly RoundedPanel _passwordField;
    private readonly Label _passwordLabel;
    private readonly Label _activationNote;
    private readonly Label _statusNote;
    private readonly Label _modeHint;
    private readonly Label _errorLabel;
    private readonly RoundedButton _createButton;
    private readonly RoundedButton _cancelButton;
    private bool _busy;
    private List<DepartmentDto> _departments = new();
    private bool _isSuperAdmin;

    public CreateUserForm(ApiClient api)
    {
        _api = api;

        Ui.StyleDialog(this);
        Text = "Ajouter un utilisateur";
        ClientSize = new Size(620, 660);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24),
            BackColor = Palette.Background,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var title = Ui.Label("Ajouter un utilisateur",
            new Font("Segoe UI", 16f, FontStyle.Bold));
        title.Location = new Point(0, 0);
        var subtitle = Ui.Label(
            "Choisissez comment le compte sera activé, puis transmettez le lien ou le mot de passe à l'utilisateur.",
            Palette.Small,
            Palette.Muted,
            autoSize: false);
        subtitle.SetBounds(0, 32, 572, 36);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        root.Controls.Add(header, 0, 0);

        var modePanel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            CornerRadius = 12,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
        };
        _directPasswordBox = new CheckBox
        {
            Text = "Définir un mot de passe immédiatement",
            Font = Palette.Body,
            ForeColor = Palette.Text,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(14, 8),
        };
        _directPasswordBox.CheckedChanged += (_, _) => UpdateMode();
        _modeHint = Ui.Label(string.Empty, Palette.Small, Palette.Muted, autoSize: false);
        _modeHint.SetBounds(14, 34, 540, 20);
        modePanel.Controls.Add(_directPasswordBox);
        modePanel.Controls.Add(_modeHint);
        root.Controls.Add(modePanel, 0, 1);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            BackColor = Color.Transparent,
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        _firstNameBox = AddTextField(fields, 0, 0, 1, "Prénom", "Ex. Awa", rightMargin: 10);
        _lastNameBox = AddTextField(fields, 0, 1, 1, "Nom", "Ex. Diallo");
        _emailBox = AddTextField(fields, 1, 0, 2, "Adresse e-mail professionnelle",
            "awa.diallo@organisation.local");
        _jobTitleBox = AddTextField(fields, 2, 0, 2, "Fonction", "Ex. Technicien support");

        _departmentCombo = AddCombo(fields, 3, 0, "Département", rightMargin: 10);
        _roleCombo = AddCombo(fields, 3, 1, "Rôle");

        var statusHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        _isActiveBox = new CheckBox
        {
            Text = Localization.T("Compte actif"),
            Checked = true,
            Font = Palette.Body,
            ForeColor = Palette.Text,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(0, 8),
        };
        _statusNote = Ui.Label("Le compte pourra se connecter après activation.",
            Palette.Small, Palette.Muted, autoSize: false);
        _statusNote.SetBounds(0, 10, 572, 20);
        statusHost.Controls.Add(_isActiveBox);
        statusHost.Controls.Add(_statusNote);
        fields.Controls.Add(statusHost, 0, 4);
        fields.SetColumnSpan(statusHost, 2);

        var passwordHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        _passwordLabel = Ui.Label("Mot de passe", Palette.Small, Palette.Muted);
        _passwordLabel.Location = new Point(0, 0);
        _passwordField = Ui.Field("8 caractères minimum", out _passwordBox,
            password: true, height: 40);
        _passwordField.Location = new Point(0, 20);
        _passwordField.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        passwordHost.Resize += (_, _) =>
            _passwordField.Width = Math.Max(100, passwordHost.Width);

        _activationNote = Ui.Label(
            "Le lien d'activation sera affiché après la création du compte.",
            Palette.Small, Palette.Muted, autoSize: false);
        _activationNote.SetBounds(0, 22, 572, 40);
        _activationNote.TextAlign = ContentAlignment.MiddleLeft;

        passwordHost.Controls.Add(_passwordLabel);
        passwordHost.Controls.Add(_passwordField);
        passwordHost.Controls.Add(_activationNote);
        fields.Controls.Add(passwordHost, 0, 5);
        fields.SetColumnSpan(passwordHost, 2);

        _errorLabel = Ui.Label(string.Empty, Palette.Small, Palette.Danger, autoSize: false);
        _errorLabel.Dock = DockStyle.Fill;
        _errorLabel.TextAlign = ContentAlignment.MiddleLeft;
        fields.Controls.Add(_errorLabel, 0, 6);
        fields.SetColumnSpan(_errorLabel, 2);

        root.Controls.Add(fields, 0, 2);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 8, 0, 0),
        };
        _createButton = Ui.Primary("Créer le compte", 150, 42);
        _createButton.Click += OnCreateClicked;
        _cancelButton = Ui.Outline("Annuler", 110, 42);
        _cancelButton.Margin = new Padding(0, 0, 10, 0);
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        footer.Controls.Add(_createButton);
        footer.Controls.Add(_cancelButton);
        root.Controls.Add(footer, 0, 3);

        Controls.Add(root);
        AcceptButton = _createButton;
        CancelButton = _cancelButton;

        Localization.Apply(this);
        UpdateMode();
        Shown += async (_, _) =>
        {
            _firstNameBox.Focus();
            try
            {
                var me = await _api.GetMeAsync();
                _isSuperAdmin = me.Role == "SuperAdmin";
                ApplyRoleAvailability();
            }
            catch
            {
            }

            try
            {
                _departments = await _api.GetDepartmentsAsync();
                _departmentCombo.Items.Clear();
                _departmentCombo.Items.Add(Localization.T("Aucun"));
                foreach (var department in _departments)
                    _departmentCombo.Items.Add(department.Name);
                _departmentCombo.SelectedIndex = 0;
            }
            catch
            {
            }
        };
    }

    public CreatedUserDto? CreatedUser { get; private set; }

    private static TextBox AddTextField(
        TableLayoutPanel grid,
        int row,
        int column,
        int columnSpan,
        string caption,
        string placeholder,
        int rightMargin = 0)
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, rightMargin, 0),
        };
        var label = Ui.Label(caption, Palette.Small, Palette.Muted);
        label.Location = new Point(0, 0);
        var field = Ui.Field(placeholder, out var box, height: 40);
        field.Location = new Point(0, 20);
        field.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        host.Resize += (_, _) => field.Width = Math.Max(100, host.Width);
        host.Controls.Add(label);
        host.Controls.Add(field);
        grid.Controls.Add(host, column, row);
        if (columnSpan > 1)
            grid.SetColumnSpan(host, columnSpan);
        return box;
    }

    private static ComboBox AddCombo(
        TableLayoutPanel grid,
        int row,
        int column,
        string caption,
        int rightMargin = 0)
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, rightMargin, 0),
        };
        var label = Ui.Label(caption, Palette.Small, Palette.Muted);
        label.Location = new Point(0, 0);
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Palette.Surface,
            ForeColor = Palette.Text,
            Font = Palette.Body,
            Location = new Point(0, 20),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
        };
        host.Resize += (_, _) => combo.Width = Math.Max(100, host.Width);
        host.Controls.Add(label);
        host.Controls.Add(combo);
        grid.Controls.Add(host, column, row);
        return combo;
    }

    private void UpdateMode()
    {
        var direct = _directPasswordBox.Checked;
        _passwordLabel.Visible = direct;
        _passwordField.Visible = direct;
        _activationNote.Visible = !direct;
        _isActiveBox.Visible = direct;
        _statusNote.Visible = !direct;
        _modeHint.Text = Localization.T(direct
            ? "Le compte sera prêt immédiatement ; transmettez le mot de passe par un canal sécurisé."
            : "Un lien sécurisé sera généré ; l'utilisateur choisira son mot de passe à sa première connexion.");
        _errorLabel.Text = string.Empty;
        if (direct)
            _passwordBox.Focus();
    }

    private void ApplyRoleAvailability()
    {
        // Seul le SuperAdmin peut désigner un administrateur ou un superviseur.
        _roleCombo.Items.Clear();
        _roleCombo.Items.Add(Localization.T("Utilisateur"));
        _roleCombo.Items.Add(Localization.T("Responsable de département"));
        if (_isSuperAdmin)
        {
            _roleCombo.Items.Add(Localization.T("Superviseur"));
            _roleCombo.Items.Add(Localization.T("Administrateur"));
        }
        _roleCombo.SelectedIndex = 0;
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;

        var firstName = _firstNameBox.Text.Trim();
        var lastName = _lastNameBox.Text.Trim();
        var email = _emailBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            _errorLabel.Text = Localization.T("Le nom et le prénom sont requis.");
            _firstNameBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') ||
            email.StartsWith('@') || email.EndsWith('@'))
        {
            _errorLabel.Text = Localization.T("Indiquez une adresse e-mail valide.");
            _emailBox.Focus();
            return;
        }

        var directPassword = _directPasswordBox.Checked;
        if (directPassword && _passwordBox.Text.Length < 8)
        {
            _errorLabel.Text = Localization.T("Le nouveau mot de passe doit contenir au moins 8 caractères.");
            _passwordBox.Focus();
            return;
        }

        _busy = true;
        _createButton.Enabled = false;
        _cancelButton.Enabled = false;
        UseWaitCursor = true;
        _errorLabel.Text = string.Empty;

        try
        {
            int? departmentId = null;
            if (_departmentCombo.SelectedIndex > 0 &&
                _departmentCombo.SelectedIndex - 1 < _departments.Count)
            {
                departmentId = _departments[_departmentCombo.SelectedIndex - 1].Id;
            }

            var roleKey = _roleCombo.SelectedIndex switch
            {
                1 => "DepartmentManager",
                2 when _isSuperAdmin => "Supervisor",
                3 when _isSuperAdmin => "OrgAdmin",
                _ => "User",
            };

            CreatedUser = await _api.CreateUserAsync(new CreateUserRequest
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                DepartmentId = departmentId,
                JobTitle = _jobTitleBox.Text.Trim(),
                Role = roleKey,
                IsActive = directPassword ? _isActiveBox.Checked : true,
                Password = directPassword ? _passwordBox.Text : string.Empty,
            });

            var result = new AccountCreatedForm(
                CreatedUser,
                _api.ResolveActivationLink(CreatedUser),
                directPassword);
            using (result)
                result.ShowDialog(this);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _errorLabel.ForeColor = Palette.Danger;
            _errorLabel.Text = Ui.FriendlyError(ex);
        }
        finally
        {
            _busy = false;
            UseWaitCursor = false;
            if (!IsDisposed)
            {
                _createButton.Enabled = true;
                _cancelButton.Enabled = true;
            }
        }
    }
}
