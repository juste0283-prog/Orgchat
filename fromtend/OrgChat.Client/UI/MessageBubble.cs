using OrgChat.Client.Models;

namespace OrgChat.Client.UI;

/// <summary>Bulles de message dessinées avec alignement gauche/droite.</summary>
public sealed class MessageBubble : Control
{
    private const int HorizontalMargin = 14;
    private const int BubblePadding = 14;
    private const int TimeHeight = 18;
    private const int AttachmentHeight = 42;
    private const int AttachmentGap = 6;
    private static readonly Font AttachmentIconFont = new("Segoe UI Emoji", 12f);

    private readonly List<(Rectangle Bounds, AttachmentDto Attachment)> _attachmentHitAreas = new();
    private AttachmentDto? _hoveredAttachment;

    public MessageBubble(MessageDto message, bool isOwn, bool showSender)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);

        Message = message;
        IsOwn = isOwn;
        ShowSender = showSender;
        Margin = Padding.Empty;
        BackColor = Palette.Background;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Copier le message", null, (_, _) => CopyToClipboard());
        ContextMenuStrip = menu;
    }

    public MessageDto Message { get; }

    public bool IsOwn { get; }

    public bool ShowSender { get; }

    public string? ReceiptText { get; set; }

    public event Action<AttachmentDto>? AttachmentRequested;

    public void Fit(int availableWidth)
    {
        Width = Math.Max(220, availableWidth);
        Height = MeasureHeight();
    }

    private int MaxBubbleWidth => Math.Max(180, (int)(Width * 0.68));

    private bool HasText => !string.IsNullOrWhiteSpace(Message.Content);

    private int AttachmentBlockHeight => Message.Attachments.Count == 0
        ? 0
        : Message.Attachments.Count * AttachmentHeight + (Message.Attachments.Count - 1) * AttachmentGap;

    private Size MeasureContent()
    {
        if (!HasText)
            return Size.Empty;

        var width = Math.Max(100, MaxBubbleWidth - BubblePadding * 2);
        return TextRenderer.MeasureText(
            Message.Content,
            Palette.Body,
            new Size(width, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
    }

    private int MeasureAttachmentWidth()
    {
        if (Message.Attachments.Count == 0)
            return 0;

        var max = 0;
        foreach (var attachment in Message.Attachments)
        {
            var nameWidth = TextRenderer.MeasureText(
                attachment.OriginalFileName, Palette.SmallBold,
                new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
            var sizeWidth = TextRenderer.MeasureText(
                attachment.DisplaySize, Palette.Small,
                new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
            max = Math.Max(max, Math.Max(nameWidth, sizeWidth) + 58);
        }

        return Math.Min(Math.Max(180, max), MaxBubbleWidth - BubblePadding * 2);
    }

    private int MeasureHeight()
    {
        var content = MeasureContent();
        var top = ShowSender ? 26 : 10;
        var gap = HasText && Message.Attachments.Count > 0 ? 8 : 0;
        return top + content.Height + gap + AttachmentBlockHeight + TimeHeight + BubblePadding + 10;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var hovered = FindAttachment(e.Location);
        if (!ReferenceEquals(hovered, _hoveredAttachment))
        {
            _hoveredAttachment = hovered;
            Cursor = hovered is null ? Cursors.Default : Cursors.Hand;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredAttachment is not null)
        {
            _hoveredAttachment = null;
            Cursor = Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var attachment = FindAttachment(e.Location);
            if (attachment is not null)
            {
                AttachmentRequested?.Invoke(attachment);
                return;
            }
        }

        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Palette.ParentBackground(this));

        _attachmentHitAreas.Clear();

        var contentSize = MeasureContent();
        var timeText = Ui.FormatMessageTime(Message.CreatedAt);
        if (Message.EditedAt is not null)
            timeText += " · modifié";
        if (!string.IsNullOrWhiteSpace(ReceiptText))
            timeText += " · " + ReceiptText;

        var timeSize = TextRenderer.MeasureText(timeText, Palette.Small);
        var attachmentWidth = MeasureAttachmentWidth();

        var bubbleWidth = Math.Min(MaxBubbleWidth,
            Math.Max(130, Math.Max(
                Math.Max(contentSize.Width, attachmentWidth) + BubblePadding * 2,
                timeSize.Width + BubblePadding * 2)));
        var bubbleHeight = Height - 6;

        var bubbleX = IsOwn
            ? Width - HorizontalMargin - bubbleWidth
            : HorizontalMargin;

        var bubbleRect = new RectangleF(bubbleX, 3, bubbleWidth, bubbleHeight);

        var fill = IsOwn ? Palette.Accent : Palette.BubbleIn;
        var border = IsOwn ? Color.Transparent : Palette.Border;
        var textColor = IsOwn ? Color.White : Palette.Text;

        Palette.DrawRounded(g, bubbleRect, 14, fill, border == Color.Transparent ? null : border);

        var x = bubbleX + BubblePadding;
        var textWidth = bubbleWidth - BubblePadding * 2;

        if (ShowSender)
        {
            TextRenderer.DrawText(g, Message.SenderUsername, Palette.SmallBold,
                new Rectangle((int)x, 10, (int)textWidth, 16),
                IsOwn ? Color.FromArgb(210, 255, 255, 255) : Palette.Accent,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        var contentY = ShowSender ? 28 : 12;
        if (HasText)
        {
            TextRenderer.DrawText(g, Message.Content, Palette.Body,
                new Rectangle((int)x, contentY, (int)textWidth, contentSize.Height + 2),
                textColor,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        }

        var attachmentY = contentY + contentSize.Height + (HasText && Message.Attachments.Count > 0 ? 8 : 0);
        foreach (var attachment in Message.Attachments)
        {
            var row = new Rectangle((int)x, attachmentY, (int)textWidth, AttachmentHeight);
            DrawAttachment(g, row, attachment, textColor);
            _attachmentHitAreas.Add((row, attachment));
            attachmentY += AttachmentHeight + AttachmentGap;
        }

        TextRenderer.DrawText(g, timeText, Palette.Small,
            new Rectangle((int)x, Height - TimeHeight - 7, (int)textWidth, 14),
            IsOwn ? Color.FromArgb(195, 255, 255, 255) : Palette.Faint,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
    }

    private void DrawAttachment(Graphics g, Rectangle row, AttachmentDto attachment, Color textColor)
    {
        var hovered = ReferenceEquals(_hoveredAttachment, attachment);
        var rowFill = IsOwn
            ? Color.FromArgb(hovered ? 70 : 42, 255, 255, 255)
            : hovered ? Palette.SurfaceHover : Palette.SurfaceAlt;
        var rowBorder = IsOwn ? Color.Transparent : Palette.Border;

        Palette.DrawRounded(g, row, 8, rowFill,
            rowBorder == Color.Transparent ? null : rowBorder);

        var iconRect = new Rectangle(row.X + 8, row.Y + 7, 28, 28);
        TextRenderer.DrawText(g, "📎", AttachmentIconFont,
            iconRect, IsOwn ? Color.White : Palette.Accent,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        var detailsX = row.X + 42;
        var detailsWidth = Math.Max(20, row.Width - 50);
        var nameColor = IsOwn ? Color.White : textColor;
        var sizeColor = IsOwn ? Color.FromArgb(205, 255, 255, 255) : Palette.Faint;

        TextRenderer.DrawText(g, attachment.OriginalFileName, Palette.SmallBold,
            new Rectangle(detailsX, row.Y + 5, detailsWidth, 17), nameColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, attachment.DisplaySize, Palette.Small,
            new Rectangle(detailsX, row.Y + 21, detailsWidth, 15), sizeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    private AttachmentDto? FindAttachment(Point location)
    {
        foreach (var (bounds, attachment) in _attachmentHitAreas)
        {
            if (bounds.Contains(location))
                return attachment;
        }

        return null;
    }

    private void CopyToClipboard()
    {
        if (string.IsNullOrWhiteSpace(Message.Content))
            return;

        try
        {
            Clipboard.SetText(Message.Content);
        }
        catch
        {
            // Le presse-papiers peut être momentanément indisponible.
        }
    }
}
