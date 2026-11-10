using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>Choix de la langue et du thème visuel du client.</summary>
public sealed class SettingsForm : Form
{
    private readonly ComboBox _languageCombo;
    private readonly ComboBox _themeCombo;
    private readonly RoundedButton _saveButton;
    private readonly RoundedButton _cancelButton;

    public SettingsForm()
    {
        Ui.StyleDialog(this);
        Text = "Paramètres";
        ClientSize = new Size(560, 390);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(26),
            BackColor = Palette.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var title = Ui.Label("Paramètres", new Font("Segoe UI", 16f, FontStyle.Bold));
        title.Location = new Point(0, 0);
        var subtitle = Ui.Label(
            "Personnalisez la langue et l'apparence de votre client OrgChat.",
            Palette.Small,
            Palette.Muted);
        subtitle.Location = new Point(0, 30);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        root.Controls.Add(header, 0, 0);

        var languageHost = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var languageLabel = Ui.Label("Langue", Palette.Small, Palette.Muted);
        languageLabel.Location = new Point(0, 0);
        _languageCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Palette.Surface,
            ForeColor = Palette.Text,
            Font = Palette.Body,
            Location = new Point(0, 22),
            Width = 320
        };
        _languageCombo.Items.Add(Localization.IsEnglish ? "French" : "Français");
        _languageCombo.Items.Add("English");
        _languageCombo.SelectedIndex = Localization.CurrentLanguage == AppLanguage.English ? 1 : 0;
        languageHost.Controls.Add(languageLabel);
        languageHost.Controls.Add(_languageCombo);
        root.Controls.Add(languageHost, 0, 1);

        var themeHost = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var themeLabel = Ui.Label("Thème", Palette.Small, Palette.Muted);
        themeLabel.Location = new Point(0, 0);
        _themeCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Palette.Surface,
            ForeColor = Palette.Text,
            Font = Palette.Body,
            Location = new Point(0, 22),
            Width = 320
        };
        foreach (var theme in ThemeManager.All)
            _themeCombo.Items.Add(Localization.T(theme.DisplayName));
        _themeCombo.SelectedIndex = Math.Max(0, ThemeManager.All
            .ToList()
            .FindIndex(theme => theme.Id == ThemeManager.CurrentTheme));
        themeHost.Controls.Add(themeLabel);
        themeHost.Controls.Add(_themeCombo);
        root.Controls.Add(themeHost, 0, 2);

        var note = Ui.Label(
            "Les changements s'appliquent immédiatement.",
            Palette.Small,
            Palette.Faint,
            autoSize: false);
        note.Dock = DockStyle.Fill;
        note.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(note, 0, 3);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 6, 0, 0)
        };
        _saveButton = Ui.Primary("Enregistrer", 140, 42);
        _saveButton.Click += OnSaveClicked;
        _cancelButton = Ui.Outline("Annuler", 110, 42);
        _cancelButton.Margin = new Padding(0, 0, 10, 0);
        _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        footer.Controls.Add(_saveButton);
        footer.Controls.Add(_cancelButton);
        root.Controls.Add(footer, 0, 4);

        Controls.Add(root);
        CancelButton = _cancelButton;
        AcceptButton = _saveButton;

        Localization.Apply(this);
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var language = _languageCombo.SelectedIndex == 1
            ? AppLanguage.English
            : AppLanguage.French;

        var themeIndex = _themeCombo.SelectedIndex;
        var theme = themeIndex >= 0 && themeIndex < ThemeManager.All.Count
            ? ThemeManager.All[themeIndex].Id
            : ThemeManager.CurrentTheme;

        try
        {
            SettingsStore.Save(new AppSettings { Language = language, Theme = theme });
            Localization.SetLanguage(language);
            ThemeManager.SetTheme(theme);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                ex.Message,
                Localization.T("Paramètres"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
