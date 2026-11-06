using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>Dialogue de modification d'un message existant.</summary>
public sealed class MessageEditForm : Form
{
    private readonly ApiClient _api;
    private readonly MessageDto _message;
    private readonly TextBox _contentBox;
    private readonly Label _errorLabel;
    private readonly RoundedButton _saveButton;
    private readonly RoundedButton _cancelButton;
    private bool _busy;

    public MessageEditForm(ApiClient api, MessageDto message)
    {
        _api = api;
        _message = message;

        Ui.StyleDialog(this);
        Text = "Modifier le message";
        ClientSize = new Size(560, 340);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24),
            BackColor = Palette.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var title = Ui.Label("Modifier le message", new Font("Segoe UI", 16f, FontStyle.Bold));
        title.Location = new Point(0, 0);
        var subtitle = Ui.Label("Le message portera la mention « modifié ».", Palette.Small, Palette.Muted);
        subtitle.Location = new Point(0, 30);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        root.Controls.Add(header, 0, 0);

        var editor = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            CornerRadius = 10,
            Padding = new Padding(12, 10, 12, 10)
        };
        _contentBox = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Palette.Surface,
            ForeColor = Palette.Text,
            Font = Palette.Body,
            Multiline = true,
            AcceptsReturn = true,
            ScrollBars = ScrollBars.Vertical,
            Text = message.Content
        };
        editor.Controls.Add(_contentBox);
        root.Controls.Add(editor, 0, 1);

        _errorLabel = Ui.Label(string.Empty, Palette.Small, Palette.Danger, autoSize: false);
        _errorLabel.Dock = DockStyle.Fill;
        root.Controls.Add(_errorLabel, 0, 2);

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
        root.Controls.Add(footer, 0, 3);

        Controls.Add(root);
        CancelButton = _cancelButton;
        Localization.Apply(this);
        Shown += (_, _) =>
        {
            _contentBox.Focus();
            _contentBox.SelectionStart = _contentBox.TextLength;
        };
    }

    public MessageDto? UpdatedMessage { get; private set; }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;

        var content = _contentBox.Text.Trim();
        if (content.Length == 0 && _message.Attachments.Count == 0)
        {
            _errorLabel.Text = Localization.T("Le message ne peut pas être vide.");
            _contentBox.Focus();
            return;
        }

        _busy = true;
        _saveButton.Enabled = false;
        _cancelButton.Enabled = false;
        UseWaitCursor = true;
        _errorLabel.Text = string.Empty;

        try
        {
            UpdatedMessage = await _api.UpdateMessageAsync(
                _message.ConversationId, _message.Id, content);
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
