using OrgChat.Client.Models;

namespace OrgChat.Client.UI;

/// <summary>Bulles de message dessinées avec alignement gauche/droite.</summary>
public sealed class MessageBubble : Control
{
    private const int HorizontalMargin = 14;
    private const int BubblePadding = 14;
    private const int TimeHeight = 18;

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

    public void Fit(int availableWidth)
    {
        Width = Math.Max(220, availableWidth);
        Height = MeasureHeight();
    }

    private int MaxBubbleWidth => Math.Max(180, (int)(Width * 0.68));

    private Size MeasureContent()
    {
        var width = Math.Max(100, MaxBubbleWidth - BubblePadding * 2);
        return TextRenderer.MeasureText(
            Message.Content,
            Palette.Body,
            new Size(width, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
    }

    private int MeasureHeight()
    {
        var content = MeasureContent();
        var top = ShowSender ? 26 : 10;
        return top + content.Height + TimeHeight + BubblePadding + 10;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Palette.ParentBackground(this));

        var contentSize = MeasureContent();
        var timeText = Ui.FormatMessageTime(Message.CreatedAt);
        if (Message.EditedAt is not null)
            timeText += " · modifié";

        var timeSize = TextRenderer.MeasureText(timeText, Palette.Small);

        var bubbleWidth = Math.Min(MaxBubbleWidth,
            Math.Max(130, Math.Max(contentSize.Width + BubblePadding * 2, timeSize.Width + BubblePadding * 2)));
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
        TextRenderer.DrawText(g, Message.Content, Palette.Body,
            new Rectangle((int)x, contentY, (int)textWidth, contentSize.Height + 2),
            textColor,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);

        TextRenderer.DrawText(g, timeText, Palette.Small,
            new Rectangle((int)x, Height - TimeHeight - 7, (int)textWidth, 14),
            IsOwn ? Color.FromArgb(195, 255, 255, 255) : Palette.Faint,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
    }

    private void CopyToClipboard()
    {
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
