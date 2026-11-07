using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>Création d'un compte par un administrateur, avec lien d'activation.</summary>
public sealed class CreateUserForm : Form
{
    private readonly ApiClient _api;
    private readonly TextBox _firstNameBox;
    private readonly TextBox _lastNameBox;
    private readonly TextBox _emailBox;
    private readonly TextBox _jobTitleBox;
    private readonly ComboBox _departmentCombo;
    private readonly ComboBox _roleCombo;
    private readonly CheckBox _isActiveBox;
    private readonly Label _errorLabel;
    private readonly RoundedButton _createButton;
    private readonly RoundedButton _cancelButton;
    private readonly TextBox _activationLinkBox;
    private readonly RoundedButton _copyLinkButton;
    private bool _busy;
    private List<DepartmentDto> _departments = new();
    private bool _isSuperAdmin;

    public CreateUserForm(ApiClient api)
    {
        _api = api;

        Ui.StyleDialog(this);
        Text = "Ajouter un utilisateur";
        ClientSize = new Size(560, 680);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 10,
            Padding = new Padding(24),
            BackColor = Palette.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        for (var i = 0; i < 6; i++)
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var title = Ui.Label("Ajouter un utilisateur", new Font("Segoe UI", 16f, FontStyle.Bold));
        title.Location = new Point(0, 0);
        var subtitle = Ui.Label(
            "Un lien d'activation sera généré. Cet utilisateur définira son mot de passe à la première connexion.",
            Palette.Small,
            Palette.Muted);
        subtitle.Location = new Point(0, 30);
        subtitle.Size = new Size(500, 28);
        subtitle.AutoSize = false;
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        root.Controls.Add(header, 0, 0);

        _firstNameBox = AddTextField(root, 1, "Prénom", "Ex. Awa");
        _lastNameBox = AddTextField(root, 2, "Nom", "Ex. Diallo");
        _emailBox = AddTextField(root, 3, "Adresse e-mail professionnelle", "awa.diallo@organisation.local");
        _jobTitleBox = AddTextField(root, 4, "Fonction", "Ex. Technicien support");

        var departmentHost = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var departmentLabel = Ui.Label("Département", Palette.Small, Palette.Muted);
        departmentLabel.Location = new Point(0, 0);
        _departmentCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Palette.Surface,
            ForeColor = Palette.Text,
            Font = Palette.Body,
            Location = new Point(0, 20),
            Width = 400
        };
        departmentHost.Controls.Add(departmentLabel);
        departmentHost.Controls.Add(_departmentCombo);
        root.Controls.Add(departmentHost, 0, 5);

        var roleHost = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var roleLabel = Ui.Label("Rôle", Palette.Small, Palette.Muted);
        roleLabel.Location = new Point(0, 0);
        _roleCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Palette.Surface,
            ForeColor = Palette.Text,
            Font = Palette.Body,
            Location = new Point(0, 20),
            Width = 400
        };
        _roleCombo.Items.AddRange(new object[]
        {
            Localization.T("Utilisateur"),
            Localization.T("Responsable de département"),
            Localization.T("Superviseur"),
            Localization.T("Administrateur"),
        });
        _roleCombo.SelectedIndex = 0;
        roleHost.Controls.Add(roleLabel);
        roleHost.Controls.Add(_roleCombo);
        root.Controls.Add(roleHost, 0, 6);

        var statusHost = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        _isActiveBox = new CheckBox
        {
            Text = Localization.T("Compte actif"),
            Checked = true,
            Font = Palette.Body,
            ForeColor = Palette.Text,
            BackColor = Palette.Background,
            AutoSize = true,
            Location = new Point(0, 6)
        };
        statusHost.Controls.Add(_isActiveBox);
        root.Controls.Add(statusHost, 0, 7);

        _errorLabel = Ui.Label(string.Empty, Palette.Small, Palette.Danger, autoSize: false);
        _errorLabel.Dock = DockStyle.Fill;
        root.Controls.Add(_errorLabel, 0, 8);

        var activationHost = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var activationLabel = Ui.Label("Lien d'activation (copiable)", Palette.Small, Palette.Muted);
        activationLabel.Location = new Point(0, 0);
        _activationLinkBox = new TextBox
        {
            ReadOnly = true,
            BackColor = Palette.Surface,
            ForeColor = Palette.Text,
            Font = Palette.Body,
            Location = new Point(0, 20),
            Width = 360,
            Height = 30,
        };
        _copyLinkButton = Ui.Outline("Copier", 110, 30);
        _copyLinkButton.Location = new Point(370, 20);
        _copyLinkButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_activationLinkBox.Text))
                return;
            Clipboard.SetText(_activationLinkBox.Text);
            _copyLinkButton.Text = Localization.T("Copié");
        };
        activationHost.Controls.Add(activationLabel);
        activationHost.Controls.Add(_activationLinkBox);
        activationHost.Controls.Add(_copyLinkButton);
        activationHost.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        root.Controls.Add(activationHost, 0, 9);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 10, 0, 0)
        };
        _createButton = Ui.Primary("Créer le compte", 150, 42);
        _createButton.Click += OnCreateClicked;
        _cancelButton = Ui.Outline("Annuler", 110, 42);
        _cancelButton.Margin = new Padding(0, 0, 10, 0);
        _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        footer.Controls.Add(_createButton);
        footer.Controls.Add(_cancelButton);
        // Row 10 is footer, activation host is row 9
        root.RowCount = 11;
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.Controls.Add(footer, 0, 10);

        Controls.Add(root);
        CancelButton = _cancelButton;
        Localization.Apply(this);
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

    public CreatedUserDto? CreatedUser { get; private set; }

    private static TextBox AddTextField(
        TableLayoutPanel root,
        int row,
        string caption,
        string placeholder,
        bool password = false)
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var label = Ui.Label(caption, Palette.Small, Palette.Muted);
        label.Location = new Point(0, 0);
        var field = Ui.Field(placeholder, out var box, password, height: 40);
        field.Location = new Point(0, 20);
        field.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        host.Resize += (_, _) => field.Width = host.Width;
        host.Controls.Add(label);
        host.Controls.Add(field);
        root.Controls.Add(host, 0, row);
        return box;
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

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
        {
            _errorLabel.Text = Localization.T("Indiquez une adresse e-mail valide.");
            _emailBox.Focus();
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
                IsActive = _isActiveBox.Checked,
            });

            _activationLinkBox.Text = CreatedUser.ActivationLink ?? CreatedUser.ActivationToken ?? string.Empty;
            _errorLabel.Text = Localization.F(
                "Compte créé : @{0}. À transmettre à l'utilisateur pour sa première connexion.",
                CreatedUser.Username);
            _errorLabel.ForeColor = Palette.Success;
            _createButton.Text = Localization.T("Fermer");
            _cancelButton.Text = Localization.T("Fermer");
            _createButton.Click -= OnCreateClicked;
            _createButton.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
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
