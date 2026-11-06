using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>Création d'un compte par un administrateur.</summary>
public sealed class CreateUserForm : Form
{
    private const int MinimumPasswordLength = 8;

    private readonly ApiClient _api;
    private readonly TextBox _usernameBox;
    private readonly TextBox _emailBox;
    private readonly TextBox _displayNameBox;
    private readonly TextBox _passwordBox;
    private readonly ComboBox _roleCombo;
    private readonly Label _errorLabel;
    private readonly RoundedButton _createButton;
    private readonly RoundedButton _cancelButton;
    private bool _busy;

    public CreateUserForm(ApiClient api)
    {
        _api = api;

        Ui.StyleDialog(this);
        Text = "Créer un compte";
        ClientSize = new Size(540, 560);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(24),
            BackColor = Palette.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        for (var i = 0; i < 5; i++)
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var title = Ui.Label("Créer un compte", new Font("Segoe UI", 16f, FontStyle.Bold));
        title.Location = new Point(0, 0);
        var subtitle = Ui.Label("Le compte pourra se connecter immédiatement.", Palette.Small, Palette.Muted);
        subtitle.Location = new Point(0, 30);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        root.Controls.Add(header, 0, 0);

        _usernameBox = AddTextField(root, 1, "Nom d'utilisateur", "Identifiant de connexion");
        _displayNameBox = AddTextField(root, 2, "Nom affiché", "Ex. Awa Diallo");
        _emailBox = AddTextField(root, 3, "Adresse e-mail", "vous@organisation.local");
        _passwordBox = AddTextField(root, 4, "Mot de passe", "8 caractères minimum", password: true);

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
            Width = 240
        };
        _roleCombo.Items.AddRange(new object[]
        {
            Localization.T("Utilisateur"),
            Localization.T("Administrateur")
        });
        _roleCombo.SelectedIndex = 0;
        roleHost.Controls.Add(roleLabel);
        roleHost.Controls.Add(_roleCombo);
        root.Controls.Add(roleHost, 0, 5);

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
        _createButton = Ui.Primary("Créer le compte", 150, 42);
        _createButton.Click += OnCreateClicked;
        _cancelButton = Ui.Outline("Annuler", 110, 42);
        _cancelButton.Margin = new Padding(0, 0, 10, 0);
        _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        footer.Controls.Add(_createButton);
        footer.Controls.Add(_cancelButton);
        root.Controls.Add(footer, 0, 7);

        Controls.Add(root);
        CancelButton = _cancelButton;
        Localization.Apply(this);
        Shown += (_, _) => _usernameBox.Focus();
    }

    public UserDto? CreatedUser { get; private set; }

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

        var username = _usernameBox.Text.Trim();
        var email = _emailBox.Text.Trim();
        var displayName = _displayNameBox.Text.Trim();
        var password = _passwordBox.Text;

        if (string.IsNullOrWhiteSpace(username))
        {
            _errorLabel.Text = Localization.T("Le nom d'utilisateur est requis.");
            _usernameBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || email.StartsWith('@') || email.EndsWith('@'))
        {
            _errorLabel.Text = Localization.T("Indiquez une adresse e-mail valide.");
            _emailBox.Focus();
            return;
        }

        if (password.Length < MinimumPasswordLength)
        {
            _errorLabel.Text = Localization.F(
                "Le mot de passe doit contenir au moins {0} caractères.",
                MinimumPasswordLength);
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
            CreatedUser = await _api.CreateUserAsync(new CreateUserRequest
            {
                Username = username,
                Email = email,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName,
                Password = password,
                Role = _roleCombo.SelectedIndex == 1 ? "OrgAdmin" : "User"
            });

            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
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
