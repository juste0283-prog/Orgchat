using OrgChat.Client.Models;

namespace OrgChat.Client.UI;

/// <summary>Élément de la liste des conversations, dessiné à la main.</summary>
public sealed class ConversationItem : Control
{
    private bool _hovered;

    public ConversationItem(ConversationDto conversation, int currentUserId)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        Conversation = conversation;
        CurrentUserId = currentUserId;
        Height = 74;
        Cursor = Cursors.Hand;
        BackColor = Palette.Sidebar;
        Margin = Padding.Empty;
    }

    public ConversationDto Conversation { get; }

    public int CurrentUserId { get; }

    public bool Selected { get; set; }

    public bool Unread { get; set; }

    public event EventHandler? Activate;

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Activate?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var background = Selected
            ? Palette.WithAlpha(Palette.Accent, 34)
            : _hovered
                ? Palette.SurfaceHover
                : Palette.Sidebar;

        using (var brush = new SolidBrush(background))
            g.FillRectangle(brush, ClientRectangle);

        if (Selected)
        {
            using var accent = new SolidBrush(Palette.Accent);
            g.FillRectangle(accent, 0, 8, 3, Height - 16);
        }

        AvatarControl.DrawAvatar(g, new Rectangle(14, 14, 42, 42),
            Conversation.DisplayTitle(CurrentUserId), Conversation.Id.ToString());

        var title = Conversation.DisplayTitle(CurrentUserId);
        var time = Ui.FormatConversationTime(Conversation.LastMessageAt ?? Conversation.CreatedAt);
        var timeWidth = string.IsNullOrEmpty(time) ? 0 : TextRenderer.MeasureText(time, Palette.Small).Width;

        var titleRect = new Rectangle(68, 12, Math.Max(30, Width - 68 - timeWidth - 34), 20);
        TextRenderer.DrawText(g, title, Palette.BodyBold, titleRect, Palette.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (timeWidth > 0)
        {
            var timeRect = new Rectangle(Width - timeWidth - 14, 14, timeWidth, 16);
            TextRenderer.DrawText(g, time, Palette.Small, timeRect, Palette.Faint,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        var preview = Conversation.PreviewText;
        var previewRect = new Rectangle(68, 36, Math.Max(30, Width - 68 - 58), 20);
        TextRenderer.DrawText(g, preview, Palette.Small, previewRect,
            Conversation.LastMessagePreview is null ? Palette.Faint : Palette.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (Conversation.IsGroup)
        {
            var badgeRect = new Rectangle(Width - 40, 44, 26, 16);
            Palette.DrawRounded(g, badgeRect, 5, Palette.WithAlpha(Palette.Accent, 40));
            TextRenderer.DrawText(g, "GRP", Palette.SmallBold, badgeRect, Palette.Accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        if (Unread)
        {
            using var dot = new SolidBrush(Palette.Accent);
            g.FillEllipse(dot, Width - 20, 20, 8, 8);
        }
    }
}
