using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>
/// Dialogue de création / modification d'un département. Reste volontairement
/// simple : nom, description, responsable et état d'activation.
/// </summary>
public sealed class DepartmentEditForm : Form
{
    private readonly TextBox _nameBox;
    private readonly TextBox _descriptionBox;
    private readonly ComboBox _managerBox;
    private readonly CheckBox _activeBox;
    private readonly List<UserDto> _users;

    public DepartmentEditForm(DepartmentDto? existing, IReadOnlyList<UserDto> users)
    {
        _users = users.ToList();

        Ui.StyleForm(this);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 430);
        BackColor = Palette.Background;
        Text = existing == null ? "Nouveau département" : "Modifier le département";

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24),
            BackColor = Palette.Background,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        var title = Ui.Label(existing == null ? "Nouveau département" : "Modifier le département",
            Palette.Title, Palette.Text, autoSize: false);
        title.Dock = DockStyle.Fill;
        root.Controls.Add(title, 0, 0);

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            BackColor = Palette.Background,
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (var i = 0; i < 4; i++)
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        AddLabel(form, "Nom", 0);
        _nameBox = AddField(form, "Nom du département", 0, 44);
        _nameBox.Text = existing?.Name ?? string.Empty;

        AddLabel(form, "Description", 1);
        _descriptionBox = AddField(form, "Description (optionnel)", 1, 82, multiline: true);
        _descriptionBox.Text = existing?.Description ?? string.Empty;
        form.RowStyles[1].Height = 98;

        AddLabel(form, "Responsable", 2);
        _managerBox = StyledCombo();
        _managerBox.Dock = DockStyle.Fill;
        _managerBox.Margin = new Padding(0, 9, 0, 9);
        _managerBox.Items.Add(Localization.T("Aucun responsable"));
        foreach (var user in _users)
            _managerBox.Items.Add($"{user.DisplayName} ({user.Username})");
        _managerBox.SelectedIndex = 0;
        if (existing?.ManagerUserId is int managerId)
        {
            var index = _users.FindIndex(u => u.Id == managerId);
            if (index >= 0)
                _managerBox.SelectedIndex = index + 1;
        }
        form.Controls.Add(_managerBox, 1, 2);

        AddLabel(form, "État", 3);
        _activeBox = new CheckBox
        {
            Text = "Département actif",
            ForeColor = Palette.Text,
            Font = Palette.Body,
            BackColor = Color.Transparent,
            Checked = existing?.IsActive ?? true,
            AutoSize = true,
            Margin = new Padding(0, 16, 0, 0),
        };
        form.Controls.Add(_activeBox, 1, 3);

        root.Controls.Add(form, 0, 1);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 10, 0, 0),
        };

        var save = Ui.Primary(existing == null ? "Créer" : "Enregistrer", 140, 40);
        save.Click += OnSaveClicked;
        footer.Controls.Add(save);

        var cancel = Ui.Outline("Annuler", 120, 40);
        cancel.Margin = new Padding(0, 0, 10, 0);
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        footer.Controls.Add(cancel);

        root.Controls.Add(footer, 0, 2);
        Controls.Add(root);

        AcceptButton = save;
        CancelButton = cancel;
        Localization.Apply(this);
    }

    public string DepartmentName => _nameBox.Text.Trim();

    public string Description => _descriptionBox.Text.Trim();

    public int? ManagerUserId => _managerBox.SelectedIndex <= 0
        ? null
        : _users[_managerBox.SelectedIndex - 1].Id;

    public bool IsActive => _activeBox.Checked;

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DepartmentName))
        {
            MessageBox.Show(this, "Le nom du département est requis.", "Département",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _nameBox.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static void AddLabel(TableLayoutPanel form, string text, int row)
    {
        var label = Ui.Label(text, Palette.SmallBold, Palette.Muted, autoSize: false);
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.Margin = new Padding(0, 0, 12, 0);
        form.Controls.Add(label, 0, row);
    }

    private static TextBox AddField(TableLayoutPanel form, string placeholder, int row, int height,
        bool multiline = false)
    {
        var host = Ui.Field(placeholder, out var box, multiline: multiline, height: height);
        host.Dock = DockStyle.Fill;
        host.Margin = new Padding(0, 9, 0, 9);
        form.Controls.Add(host, 1, row);
        return box;
    }

    private static ComboBox StyledCombo() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        BackColor = Palette.Surface,
        ForeColor = Palette.Text,
        Font = Palette.Body,
    };
}
