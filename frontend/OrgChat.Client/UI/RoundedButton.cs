using System.Drawing.Drawing2D;

namespace OrgChat.Client.UI;

/// <summary>Bouton plat dessiné avec des coins arrondis et un état de survol.</summary>
public sealed class RoundedButton : Button
{
    private bool _hovered;
    private bool _pressed;

    public RoundedButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Font = Palette.BodyBold;
        ForeColor = Palette.OnAccent;
        Height = 40;
    }

    public int CornerRadius { get; set; } = 9;

    public Color NormalColor { get; set; } = Palette.Accent;

    public Color HoverColor { get; set; } = Palette.AccentHover;

    public Color PressedColor { get; set; } = Palette.Accent;

    public Color DisabledColor { get; set; } = Palette.SurfaceAlt;

    public Color BorderColor { get; set; } = Color.Transparent;

    public Color HoverBorderColor { get; set; } = Color.Transparent;

    public bool Outline { get; set; }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var graphics = pevent.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Palette.ParentBackground(this));

        var fill = !Enabled
            ? DisabledColor
            : _pressed
                ? PressedColor
                : _hovered
                    ? HoverColor
                    : NormalColor;

        if (Outline && Enabled)
            fill = _hovered ? Palette.SurfaceHover : Palette.Surface;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        var border = Outline
            ? (_hovered ? Palette.Accent : Palette.Border)
            : BorderColor;

        Palette.DrawRounded(graphics, rect, CornerRadius, fill,
            border == Color.Transparent ? null : border);

        var textColor = Enabled
            ? (Outline ? Palette.Text : ForeColor)
            : Palette.Faint;

        TextRenderer.DrawText(
            graphics,
            Text,
            Font,
            ClientRectangle,
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
