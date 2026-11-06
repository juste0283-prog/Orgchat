using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>Changement de mot de passe personnel ou réinitialisation par un administrateur.</summary>
public sealed class ChangePasswordForm : Form
{
    private const int MinimumPasswordLength = 8;

    private readonly ApiClient _api;
    private readonly int _userId;
    private readonly bool _adminReset;
    private readonly TextBox _currentBox;
    private readonly TextBox _newBox;
    private readonly TextBox _confirmBox;
    private readonly Label _errorLabel;
    private readonly RoundedButton _saveButton;
    private readonly RoundedButton _cancelButton;
    private bool _busy;

    public ChangePasswordForm(ApiClient api, int userId, string username, bool adminReset)
    {
        _api = api;
        _userId = userId;
        _adminReset = adminReset;

        Ui.StyleDialog(this);
        Text = adminReset ? "Réinitialiser le mot de passe" : "Changer mon mot de passe";
        ClientSize = new Size(520, adminReset ? 380 : 460);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = adminReset ? 5 : 6,
            Padding = new Padding(24),
            BackColor = Palette.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        if (!adminReset)
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var title = Ui.Label(
            adminReset ? "Réinitialiser le mot de passe" : "Changer mon mot de passe",
            new Font("Segoe UI", 16f, FontStyle.Bold));
        title.Location = new Point(0, 0);
        var subtitleText = adminReset
            ? $"Définissez un nouveau mot de passe pour @{username}."
            : "Choisissez un mot de passe d'au moins 8 caractères.";
        var subtitle = Ui.Label(subtitleText, Palette.Small, Palette.Muted);
        subtitle.Location = new Point(0, 30);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        root.Controls.Add(header, 0, 0);

        var row = 1;
        if (!adminReset)
        {
            _currentBox = AddPasswordField(root, row++, "Mot de passe actuel", "Votre mot de passe actuel");
        }
        else
        {
            _currentBox = null!;
        }

        _newBox = AddPasswordField(root, row++, "Nouveau mot de passe", "Au moins 8 caractères");
        _confirmBox = AddPasswordField(root, row++, "Confirmer le nouveau mot de passe", "Répétez le mot de passe");

        _errorLabel = Ui.Label(string.Empty, Palette.Small, Palette.Danger, autoSize: false);
        _errorLabel.Dock = DockStyle.Fill;
        root.Controls.Add(_errorLabel, 0, row++);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 10, 0, 0)
        };
        _saveButton = Ui.Primary("Enregistrer", 130, 42);
        _saveButton.Click += OnSaveClicked;
        _cancelButton = Ui.Outline("Annuler", 110, 42);
        _cancelButton.Margin = new Padding(0, 0, 10, 0);
        _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        footer.Controls.Add(_saveButton);
        footer.Controls.Add(_cancelButton);
        root.Controls.Add(footer, 0, row);

        Controls.Add(root);
        CancelButton = _cancelButton;
        Shown += (_, _) => (_currentBox ?? _newBox).Focus();
    }

    private static TextBox AddPasswordField(TableLayoutPanel root, int row, string caption, string placeholder)
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var label = Ui.Label(caption, Palette.Small, Palette.Muted);
        label.Location = new Point(0, 0);
        var field = Ui.Field(placeholder, out var box, password: true, height: 40);
        field.Location = new Point(0, 20);
        field.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        host.Resize += (_, _) => field.Width = host.Width;
        host.Controls.Add(label);
        host.Controls.Add(field);
        root.Controls.Add(host, 0, row);
        return box;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;

        if (!_adminReset && string.IsNullOrEmpty(_currentBox.Text))
        {
            _errorLabel.Text = "Indiquez votre mot de passe actuel.";
            _currentBox.Focus();
            return;
        }

        if (_newBox.Text.Length < MinimumPasswordLength)
        {
            _errorLabel.Text = $"Le nouveau mot de passe doit contenir au moins {MinimumPasswordLength} caractères.";
            _newBox.Focus();
            return;
        }

        if (!string.Equals(_newBox.Text, _confirmBox.Text, StringComparison.Ordinal))
        {
            _errorLabel.Text = "Les deux mots de passe ne correspondent pas.";
            _confirmBox.Focus();
            return;
        }

        _busy = true;
        _saveButton.Enabled = false;
        _cancelButton.Enabled = false;
        UseWaitCursor = true;
        _errorLabel.Text = string.Empty;

        try
        {
            if (_adminReset)
            {
                await _api.ResetUserPasswordAsync(_userId, new ResetPasswordRequest
                {
                    NewPassword = _newBox.Text
                });
            }
            else
            {
                await _api.ChangeMyPasswordAsync(new ChangePasswordRequest
                {
                    CurrentPassword = _currentBox.Text,
                    NewPassword = _newBox.Text
                });
            }

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
                _saveButton.Enabled = true;
                _cancelButton.Enabled = true;
            }
        }
    }
}
