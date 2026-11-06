using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>Recherche plein texte dans les messages accessibles à l'utilisateur.</summary>
public sealed class MessageSearchForm : Form
{
    private readonly ApiClient _api;
    private readonly TextBox _queryBox;
    private readonly ListBox _resultsBox;
    private readonly Label _statusLabel;
    private readonly RoundedButton _searchButton;
    private readonly RoundedButton _closeButton;
    private bool _busy;

    public MessageSearchForm(ApiClient api, string initialQuery = "")
    {
        _api = api;

        Ui.StyleDialog(this);
        Text = "Rechercher dans les messages";
        ClientSize = new Size(760, 620);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(24),
            BackColor = Palette.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var title = Ui.Label("Rechercher dans les messages", new Font("Segoe UI", 16f, FontStyle.Bold));
        title.Location = new Point(0, 0);
        var subtitle = Ui.Label(
            "Retrouvez un message dans toutes vos conversations. Double-cliquez sur un résultat pour l'ouvrir.",
            Palette.Small,
            Palette.Muted);
        subtitle.Location = new Point(0, 30);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        root.Controls.Add(header, 0, 0);

        var searchRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Palette.Background,
            Margin = Padding.Empty
        };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));

        var field = Ui.Field("Rechercher un mot ou une phrase…", out _queryBox, height: 42);
        field.Dock = DockStyle.Fill;
        field.Margin = new Padding(0, 0, 12, 0);
        _queryBox.Text = initialQuery;
        _queryBox.KeyDown += OnQueryKeyDown;
        searchRow.Controls.Add(field, 0, 0);

        _searchButton = Ui.Primary("Rechercher", 120, 42);
        _searchButton.Dock = DockStyle.Fill;
        _searchButton.Click += async (_, _) => await SearchAsync();
        searchRow.Controls.Add(_searchButton, 1, 0);
        root.Controls.Add(searchRow, 0, 1);

        _statusLabel = Ui.Label(
            "Saisissez au moins 2 caractères.",
            Palette.Small,
            Palette.Faint,
            autoSize: false);
        _statusLabel.Dock = DockStyle.Fill;
        root.Controls.Add(_statusLabel, 0, 2);

        _resultsBox = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Palette.Surface,
            ForeColor = Palette.Text,
            Font = Palette.Body,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 68,
            IntegralHeight = false
        };
        _resultsBox.DrawItem += OnDrawItem;
        _resultsBox.DoubleClick += OnResultActivated;
        _resultsBox.KeyDown += OnResultKeyDown;
        root.Controls.Add(_resultsBox, 0, 3);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 10, 0, 0)
        };
        _closeButton = Ui.Outline("Fermer", 110, 42);
        _closeButton.Click += (_, _) => Close();
        footer.Controls.Add(_closeButton);
        root.Controls.Add(footer, 0, 4);

        Controls.Add(root);
        CancelButton = _closeButton;
        Localization.Apply(this);
        Shown += async (_, _) =>
        {
            if (initialQuery.Trim().Length >= 2)
                await SearchAsync();
            else
                _queryBox.Focus();
        };
    }

    public int? SelectedConversationId { get; private set; }

    private async Task SearchAsync()
    {
        if (_busy)
            return;

        var query = _queryBox.Text.Trim();
        if (query.Length < 2)
        {
            _statusLabel.Text = Localization.T("Saisissez au moins 2 caractères.");
            _resultsBox.Items.Clear();
            return;
        }

        _busy = true;
        _searchButton.Enabled = false;
        _statusLabel.Text = Localization.T("Recherche…");
        UseWaitCursor = true;

        try
        {
            var results = await _api.SearchMessagesAsync(query);
            _resultsBox.BeginUpdate();
            _resultsBox.Items.Clear();
            foreach (var result in results)
                _resultsBox.Items.Add(result);
            _resultsBox.EndUpdate();

            _statusLabel.Text = results.Count == 0
                ? Localization.T("Aucun message trouvé.")
                : Localization.F("{0} résultat(s) — double-cliquez pour ouvrir.", results.Count);
        }
        catch (Exception ex)
        {
            _resultsBox.Items.Clear();
            _statusLabel.Text = Ui.FriendlyError(ex);
        }
        finally
        {
            _busy = false;
            _searchButton.Enabled = true;
            UseWaitCursor = false;
        }
    }

    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
            return;

        e.SuppressKeyPress = true;
        e.Handled = true;
        _ = SearchAsync();
    }

    private void OnResultActivated(object? sender, EventArgs e)
    {
        if (_resultsBox.SelectedItem is not MessageSearchResultDto result)
            return;

        SelectedConversationId = result.ConversationId;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void OnResultKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
            return;

        e.SuppressKeyPress = true;
        e.Handled = true;
        OnResultActivated(sender, EventArgs.Empty);
    }

    private void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _resultsBox.Items.Count)
            return;

        var result = (MessageSearchResultDto)_resultsBox.Items[e.Index]!;
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var background = selected ? Palette.AccentSoft : Palette.Surface;
        using (var brush = new SolidBrush(background))
            e.Graphics.FillRectangle(brush, e.Bounds);

        var contentBounds = new Rectangle(e.Bounds.X + 12, e.Bounds.Y + 7, e.Bounds.Width - 24, e.Bounds.Height - 14);
        TextRenderer.DrawText(
            e.Graphics,
            $"{result.ConversationTitle} · {result.SenderName}",
            Palette.SmallBold,
            new Rectangle(contentBounds.X, contentBounds.Y, contentBounds.Width - 90, 18),
            Palette.Accent,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        TextRenderer.DrawText(
            e.Graphics,
            result.LocalCreatedAt.ToString("dd/MM/yyyy HH:mm"),
            Palette.Small,
            new Rectangle(contentBounds.Right - 120, contentBounds.Y, 120, 18),
            Palette.Faint,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

        TextRenderer.DrawText(
            e.Graphics,
            result.Content.Replace('\r', ' ').Replace('\n', ' '),
            Palette.Body,
            new Rectangle(contentBounds.X, contentBounds.Y + 22, contentBounds.Width, 20),
            Palette.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        using var pen = new Pen(Palette.Border);
        e.Graphics.DrawLine(pen, contentBounds.Left, e.Bounds.Bottom - 1, contentBounds.Right, e.Bounds.Bottom - 1);
    }
}
