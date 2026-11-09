using System.Drawing.Drawing2D;

namespace OrgChat.Client.UI;

/// <summary>
/// Élément de navigation de la barre latérale bleu nuit : fond arrondi,
/// icône à gauche et libellé aligné. L'élément actif est peint avec la
/// couleur d'accent du thème.
/// </summary>
public sealed class SidebarNavButton : Control
{
    private bool _hovered;
    private bool _pressed;
    private bool _active;

    public SidebarNavButton(string glyph, string text)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);

        Glyph = glyph;
        LabelText = text;
        Height = 42;
        Width = 210;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        Font = Palette.Body;
        TabStop = false;
    }

    public string Glyph { get; set; }

    public string LabelText { get; set; }

    public bool Active
    {
        get => _active;
        set { _active = value; Invalidate(); }
    }

    public Color ActiveColor { get; set; } = Palette.Accent;

    public Color HoverColor { get; set; } = Palette.DashboardSidebarHover;

    public Color TextColor { get; set; } = Palette.DashboardSidebarText;

    public Color InactiveTextColor { get; set; } = ColorTranslator.FromHtml("#CBD6E6");

    public Color MutedTextColor { get; set; } = Palette.DashboardSidebarMuted;

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

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Palette.ParentBackground(this));

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        var fill = Active
            ? ActiveColor
            : _hovered && Enabled
                ? HoverColor
                : Color.Transparent;

        if (fill != Color.Transparent)
        {
            var draw = _pressed && Enabled && !Active ? Palette.Blend(fill, Color.Black, 0.10f) : fill;
            Palette.DrawRounded(g, rect, 9, draw);
        }

        var glyphColor = !Enabled
            ? MutedTextColor
            : Active
                ? Color.White
                : ColorTranslator.FromHtml("#AFC0D8");

        using (var glyphFont = new Font("Segoe UI Symbol", 12.5f, FontStyle.Regular))
        {
            TextRenderer.DrawText(
                g,
                Glyph,
                glyphFont,
                new Rectangle(14, 0, 24, Height),
                glyphColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        var textColor = !Enabled
            ? MutedTextColor
            : Active
                ? TextColor
                : InactiveTextColor;

        var font = Active ? Palette.BodyBold : Palette.Body;
        TextRenderer.DrawText(
            g,
            LabelText,
            font,
            new Rectangle(46, 0, Math.Max(10, Width - 58), Height),
            textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}
