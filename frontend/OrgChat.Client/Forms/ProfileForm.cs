using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>Consultation et modification du profil de l'utilisateur connecté.</summary>
public sealed class ProfileForm : Form
{
    private readonly ApiClient _api;
    private readonly UserDto _user;
    private readonly TextBox _displayNameBox;
    private readonly TextBox _emailBox;
    private readonly Label _errorLabel;
    private readonly RoundedButton _saveButton;
    private readonly RoundedButton _cancelButton;
    private readonly RoundedButton _passwordButton;
    private bool _busy;

    public ProfileForm(ApiClient api, UserDto user)
    {
        _api = api;
        _user = user;

        Ui.StyleDialog(this);
        Text = "Mon profil";
        ClientSize = new Size(500, 520);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(28),
            BackColor = Palette.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var avatar = new AvatarControl
        {
            DisplayName = user.DisplayName,
            Seed = user.Id.ToString(),
            Location = new Point(0, 4),
            Size = new Size(68, 68)
        };
        header.Controls.Add(avatar);

        var name = Ui.Label(string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName,
            new Font("Segoe UI", 15f, FontStyle.Bold));
        name.Location = new Point(84, 8);
        header.Controls.Add(name);

        var handle = Ui.Label("@" + user.Username, Palette.Body, Palette.Muted);
        handle.Location = new Point(84, 34);
        header.Controls.Add(handle);

        var role = Ui.Pill(user.IsAdmin ? "Administrateur" : "Utilisateur", user.IsAdmin ? Palette.Warning : Palette.Accent);
        role.Location = new Point(84, 58);
        header.Controls.Add(role);
        root.Controls.Add(header, 0, 0);

        var displayRow = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var displayLabel = Ui.Label("Nom affiché", Palette.Small, Palette.Muted);
        displayLabel.Location = new Point(0, 0);
        var displayField = Ui.Field("Votre nom", out _displayNameBox, height: 40);
        displayField.Location = new Point(0, 20);
        displayField.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        displayRow.Resize += (_, _) => displayField.Width = displayRow.Width;
        displayRow.Controls.Add(displayLabel);
        displayRow.Controls.Add(displayField);
        root.Controls.Add(displayRow, 0, 1);

        var emailRow = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var emailLabel = Ui.Label("Adresse e-mail", Palette.Small, Palette.Muted);
        emailLabel.Location = new Point(0, 0);
        var emailField = Ui.Field("vous@organisation.local", out _emailBox, height: 40);
        emailField.Location = new Point(0, 20);
        emailField.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        emailRow.Resize += (_, _) => emailField.Width = emailRow.Width;
        emailRow.Controls.Add(emailLabel);
        emailRow.Controls.Add(emailField);
        root.Controls.Add(emailRow, 0, 2);

        var info = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        _errorLabel = Ui.Label(string.Empty, Palette.Small, Palette.Danger, autoSize: false);
        _errorLabel.Dock = DockStyle.Top;
        _errorLabel.Height = 32;
        info.Controls.Add(_errorLabel);

        var note = Ui.Label(
            "Le nom d'utilisateur et le rôle ne peuvent pas être modifiés depuis cet écran.",
            Palette.Small,
            Palette.Faint,
            autoSize: false);
        note.Dock = DockStyle.Top;
        note.Height = 34;
        info.Controls.Add(note);

        _passwordButton = Ui.Outline("Changer mon mot de passe", 220, 36);
        _passwordButton.Location = new Point(0, 88);
        _passwordButton.Click += OnChangePasswordClicked;
        info.Controls.Add(_passwordButton);
        root.Controls.Add(info, 0, 3);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 6, 0, 0)
        };
        _saveButton = Ui.Primary("Enregistrer", 130, 40);
        _saveButton.Click += OnSaveClicked;
        _cancelButton = Ui.Outline("Annuler", 110, 40);
        _cancelButton.Margin = new Padding(0, 0, 10, 0);
        _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        footer.Controls.Add(_saveButton);
        footer.Controls.Add(_cancelButton);
        root.Controls.Add(footer, 0, 4);

        Controls.Add(root);
        CancelButton = _cancelButton;
        Localization.Apply(this);

        _displayNameBox.Text = user.DisplayName;
        _emailBox.Text = user.Email;
        Shown += (_, _) => _displayNameBox.Focus();
    }

    public UserDto? UpdatedUser { get; private set; }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;

        var displayName = _displayNameBox.Text.Trim();
        var email = _emailBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(displayName))
        {
            ShowError(Localization.T("Le nom affiché est requis."));
            _displayNameBox.Focus();
            return;
        }

        if (!string.IsNullOrWhiteSpace(email) &&
            (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@')))
        {
            ShowError(Localization.T("Saisissez une adresse e-mail valide."));
            _emailBox.Focus();
            return;
        }

        _busy = true;
        SetBusy(true);
        ShowError(string.Empty);

        try
        {
            UpdatedUser = await _api.UpdateUserAsync(_user.Id, new UpdateUserRequest
            {
                DisplayName = displayName,
                Email = email
            });
            DialogResult = DialogResult.OK;
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

    private void OnChangePasswordClicked(object? sender, EventArgs e)
    {
        using var dialog = new ChangePasswordForm(_api, _user.Id, _user.Username, adminReset: false);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        ShowError(string.Empty);
        MessageBox.Show(this,
            Localization.T("Votre mot de passe a été modifié."),
            Localization.T("Mot de passe modifié"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void SetBusy(bool busy)
    {
        _saveButton.Enabled = !busy;
        _cancelButton.Enabled = !busy;
        _passwordButton.Enabled = !busy;
        _saveButton.Text = Localization.T(busy ? "Enregistrement…" : "Enregistrer");
        _displayNameBox.Enabled = !busy;
        _emailBox.Enabled = !busy;
        UseWaitCursor = busy;
    }

    private void ShowError(string message) => _errorLabel.Text = message;
}
