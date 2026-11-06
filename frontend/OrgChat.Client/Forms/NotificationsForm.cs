using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>Historique persistant des notifications de l'utilisateur.</summary>
public sealed class NotificationsForm : Form
{
    private readonly ApiClient _api;
    private readonly SmoothFlowPanel _listPanel;
    private readonly Label _subtitle;
    private readonly Label _errorLabel;
    private readonly RoundedButton _markAllButton;
    private readonly RoundedButton _clearButton;
    private readonly RoundedButton _loadMoreButton;

    private List<NotificationDto> _notifications = new();
    private bool _busy;
    private bool _hasMore;
    private const int PageSize = 50;

    public NotificationsForm(ApiClient api)
    {
        _api = api;

        Ui.StyleDialog(this);
        Text = "Notifications";
        ClientSize = new Size(680, 620);

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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var title = Ui.Label("Notifications", new Font("Segoe UI", 16f, FontStyle.Bold));
        title.Location = new Point(0, 0);
        _subtitle = Ui.Label("Chargement…", Palette.Small, Palette.Muted);
        _subtitle.Location = new Point(0, 30);
        header.Controls.Add(title);
        header.Controls.Add(_subtitle);
        root.Controls.Add(header, 0, 0);

        _listPanel = new SmoothFlowPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Palette.Background,
            Padding = new Padding(0, 4, 0, 4)
        };
        _listPanel.Resize += (_, _) => ResizeRows();
        root.Controls.Add(_listPanel, 0, 1);

        _errorLabel = Ui.Label(string.Empty, Palette.Small, Palette.Danger, autoSize: false);
        _errorLabel.Dock = DockStyle.Fill;
        root.Controls.Add(_errorLabel, 0, 2);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 8, 0, 0)
        };

        var closeButton = Ui.Outline("Fermer", 100, 40);
        closeButton.Click += (_, _) => Close();
        footer.Controls.Add(closeButton);

        _clearButton = Ui.Danger("Tout supprimer", 140, 40);
        _clearButton.Margin = new Padding(0, 0, 10, 0);
        _clearButton.Click += async (_, _) => await ClearAllAsync();
        footer.Controls.Add(_clearButton);

        _markAllButton = Ui.Primary("Tout marquer comme lu", 190, 40);
        _markAllButton.Margin = new Padding(0, 0, 10, 0);
        _markAllButton.Click += async (_, _) => await MarkAllReadAsync();
        footer.Controls.Add(_markAllButton);

        _loadMoreButton = Ui.Outline("Charger plus", 130, 40);
        _loadMoreButton.Margin = new Padding(0, 0, 10, 0);
        _loadMoreButton.Visible = false;
        _loadMoreButton.Click += async (_, _) => await LoadMoreAsync();
        footer.Controls.Add(_loadMoreButton);

        root.Controls.Add(footer, 0, 3);

        Controls.Add(root);
        CancelButton = closeButton;

        Shown += async (_, _) => await LoadAsync();
    }

    public int? SelectedConversationId { get; private set; }

    private async Task LoadAsync()
    {
        if (_busy)
            return;

        _busy = true;
        _errorLabel.Text = string.Empty;
        UseWaitCursor = true;

        try
        {
            var page = await _api.GetNotificationsAsync(PageSize, 0);
            _notifications = page.OrderByDescending(n => n.CreatedAt).ToList();
            _hasMore = page.Count == PageSize;
            RebuildRows();
        }
        catch (Exception ex)
        {
            _errorLabel.Text = Ui.FriendlyError(ex);
            _subtitle.Text = "Impossible de charger les notifications.";
        }
        finally
        {
            _busy = false;
            UseWaitCursor = false;
        }
    }

    private async Task LoadMoreAsync()
    {
        if (_busy || !_hasMore)
            return;

        _busy = true;
        _errorLabel.Text = string.Empty;
        UseWaitCursor = true;

        try
        {
            var page = await _api.GetNotificationsAsync(PageSize, _notifications.Count);
            var knownIds = _notifications.Select(n => n.Id).ToHashSet();
            var added = page.Where(n => knownIds.Add(n.Id)).ToList();
            _notifications.AddRange(added);
            _notifications = _notifications.OrderByDescending(n => n.CreatedAt).ToList();
            _hasMore = page.Count == PageSize;
            RebuildRows();
        }
        catch (Exception ex)
        {
            _errorLabel.Text = Ui.FriendlyError(ex);
        }
        finally
        {
            _busy = false;
            UseWaitCursor = false;
        }
    }

    private void RebuildRows()
    {
        _listPanel.SuspendLayout();
        _listPanel.Controls.Clear();

        foreach (var notification in _notifications)
            _listPanel.Controls.Add(BuildRow(notification));

        if (_notifications.Count == 0)
        {
            var empty = Ui.Label("Aucune notification pour le moment.", Palette.Small, Palette.Faint);
            empty.Margin = new Padding(4, 16, 0, 0);
            _listPanel.Controls.Add(empty);
        }

        var unread = _notifications.Count(n => !n.IsRead);
        _subtitle.Text = unread > 0
            ? $"{_notifications.Count} notification(s) affichée(s) · {unread} non lue(s)"
            : $"{_notifications.Count} notification(s) affichée(s) · tout est lu";
        _markAllButton.Enabled = unread > 0;
        _clearButton.Enabled = _notifications.Count > 0;
        _loadMoreButton.Visible = _hasMore;

        _listPanel.ResumeLayout();
        ResizeRows();
    }

    private Control BuildRow(NotificationDto notification)
    {
        var unread = !notification.IsRead;
        var row = new RoundedPanel
        {
            Height = 80,
            Width = Math.Max(280, _listPanel.ClientSize.Width - 20),
            FillColor = unread
                ? Palette.Blend(Palette.Surface, Palette.Accent, 0.08f)
                : Palette.Surface,
            BorderColor = unread ? Palette.WithAlpha(Palette.Accent, 100) : Palette.Border,
            CornerRadius = 10,
            Margin = new Padding(0, 0, 0, 8),
            Cursor = Cursors.Hand
        };

        if (unread)
        {
            row.Controls.Add(new Panel
            {
                Location = new Point(0, 10),
                Size = new Size(3, 60),
                BackColor = Palette.Accent
            });
        }

        var avatar = new AvatarControl
        {
            DisplayName = notification.SenderName,
            Seed = notification.SenderId.ToString(),
            Location = new Point(14, 19),
            Size = new Size(42, 42),
            Cursor = Cursors.Hand
        };
        row.Controls.Add(avatar);

        var time = Ui.FormatConversationTime(notification.CreatedAt);
        var timeWidth = string.IsNullOrEmpty(time)
            ? 0
            : TextRenderer.MeasureText(time, Palette.Small).Width;

        var titleText = string.IsNullOrWhiteSpace(notification.ConversationTitle)
            ? notification.SenderName
            : notification.ConversationTitle;
        if (string.IsNullOrWhiteSpace(titleText))
            titleText = "Nouveau message";

        var title = Ui.Label(titleText, Palette.BodyBold,
            unread ? Palette.Text : Palette.Muted, autoSize: false);
        title.Location = new Point(68, 13);
        title.Height = 20;
        title.Cursor = Cursors.Hand;
        title.Tag = timeWidth + 70;
        row.Controls.Add(title);

        var sender = string.IsNullOrWhiteSpace(notification.SenderName)
            ? string.Empty
            : notification.SenderName + " · ";
        var preview = string.IsNullOrWhiteSpace(notification.Preview)
            ? "Nouveau message"
            : notification.Preview;
        var detail = Ui.Label(sender + preview, Palette.Small,
            unread ? Palette.Muted : Palette.Faint, autoSize: false);
        detail.Location = new Point(68, 36);
        detail.Height = 20;
        detail.Cursor = Cursors.Hand;
        row.Controls.Add(detail);

        if (timeWidth > 0)
        {
            var timeLabel = Ui.Label(time, Palette.Small, Palette.Faint, autoSize: false);
            timeLabel.Location = new Point(row.Width - timeWidth - 56, 15);
            timeLabel.Size = new Size(timeWidth, 16);
            timeLabel.TextAlign = ContentAlignment.MiddleRight;
            timeLabel.Cursor = Cursors.Hand;
            row.Controls.Add(timeLabel);
        }

        title.Width = Math.Max(80, row.Width - 68 - timeWidth - 70);
        detail.Tag = 24;
        detail.Width = Math.Max(80, row.Width - 68 - 24);

        EventHandler handler = async (_, _) => await OpenAsync(notification);
        WireClick(row, handler);

        var deleteButton = Ui.Ghost("×", 28, 28);
        deleteButton.Location = new Point(row.Width - 40, 8);
        deleteButton.Tag = "delete";
        deleteButton.Click += async (_, _) => await DeleteNotificationAsync(notification);
        row.Controls.Add(deleteButton);

        return row;
    }

    private static void WireClick(Control control, EventHandler handler)
    {
        control.Click += handler;
        foreach (Control child in control.Controls)
            WireClick(child, handler);
    }

    private async Task OpenAsync(NotificationDto notification)
    {
        if (_busy)
            return;

        _errorLabel.Text = string.Empty;

        if (!notification.IsRead)
        {
            _busy = true;
            UseWaitCursor = true;
            try
            {
                var updated = await _api.MarkNotificationReadAsync(notification.Id);
                notification.ReadAt = updated.ReadAt ?? DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _errorLabel.Text = Ui.FriendlyError(ex);
                return;
            }
            finally
            {
                _busy = false;
                UseWaitCursor = false;
            }
        }

        if (notification.ConversationId <= 0)
        {
            RebuildRows();
            return;
        }

        SelectedConversationId = notification.ConversationId;
        DialogResult = DialogResult.OK;
        Close();
    }

    private async Task MarkAllReadAsync()
    {
        if (_busy)
            return;

        _errorLabel.Text = string.Empty;
        _busy = true;
        UseWaitCursor = true;

        try
        {
            await _api.MarkAllNotificationsReadAsync();
            var readAt = DateTime.UtcNow;
            foreach (var notification in _notifications)
                notification.ReadAt ??= readAt;
            RebuildRows();
        }
        catch (Exception ex)
        {
            _errorLabel.Text = Ui.FriendlyError(ex);
        }
        finally
        {
            _busy = false;
            UseWaitCursor = false;
        }
    }

    private async Task DeleteNotificationAsync(NotificationDto notification)
    {
        if (_busy)
            return;

        _busy = true;
        UseWaitCursor = true;
        _errorLabel.Text = string.Empty;

        try
        {
            await _api.DeleteNotificationAsync(notification.Id);
            _notifications.RemoveAll(n => n.Id == notification.Id);
            RebuildRows();
        }
        catch (Exception ex)
        {
            _errorLabel.Text = Ui.FriendlyError(ex);
        }
        finally
        {
            _busy = false;
            UseWaitCursor = false;
        }
    }

    private async Task ClearAllAsync()
    {
        if (_busy || _notifications.Count == 0)
            return;

        var confirm = MessageBox.Show(this,
            "Supprimer toutes vos notifications ? Cette action est définitive.",
            "Tout supprimer",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
            return;

        _busy = true;
        UseWaitCursor = true;
        _errorLabel.Text = string.Empty;

        try
        {
            await _api.ClearNotificationsAsync();
            _notifications.Clear();
            _hasMore = false;
            RebuildRows();
        }
        catch (Exception ex)
        {
            _errorLabel.Text = Ui.FriendlyError(ex);
        }
        finally
        {
            _busy = false;
            UseWaitCursor = false;
        }
    }

    private void ResizeRows()
    {
        var width = Math.Max(280, _listPanel.ClientSize.Width - 20);
        foreach (Control control in _listPanel.Controls)
        {
            if (control is not RoundedPanel row)
                continue;

            row.Width = width;
            foreach (Control child in row.Controls)
            {
                if (child is RoundedButton { Tag: "delete" } deleteButton)
                {
                    deleteButton.Left = width - deleteButton.Width - 12;
                    continue;
                }

                if (child is Label { AutoSize: false } label)
                {
                    if (label.TextAlign == ContentAlignment.MiddleRight)
                    {
                        label.Left = width - label.Width - 56;
                    }
                    else
                    {
                        var rightPadding = label.Tag is int padding ? padding : 24;
                        label.Width = Math.Max(80, width - label.Left - rightPadding);
                    }
                }
            }
        }
    }
}
