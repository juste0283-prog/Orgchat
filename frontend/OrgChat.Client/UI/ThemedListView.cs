using System.Drawing.Drawing2D;

namespace OrgChat.Client.UI;

/// <summary>
/// Liste détaillée entièrement dessinée selon le thème actif : en-têtes
/// sombres, lignes alternées, sélection douce et survol discret. Évite le
/// rendu système clair qui détonnait dans les thèmes sombres.
/// </summary>
public sealed class ThemedListView : ListView
{
    private int _hoveredIndex = -1;

    public ThemedListView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        View = View.Details;
        FullRowSelect = true;
        OwnerDraw = true;
        BorderStyle = BorderStyle.None;
        HeaderStyle = ColumnHeaderStyle.Nonclickable;
        MultiSelect = false;
        HideSelection = false;
        GridLines = false;
        BackColor = Palette.Surface;
        ForeColor = Palette.Text;
        Font = Palette.Body;
    }

    public Color AlternateRowColor { get; set; } = Palette.Blend(Palette.Surface, Palette.Background, 0.35f);

    public Color HeaderColor { get; set; } = Palette.SurfaceAlt;

    public Color HoverColor { get; set; } = Palette.Blend(Palette.Surface, Palette.Accent, 0.10f);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var item = GetItemAt(e.X, e.Y);
        var index = item?.Index ?? -1;
        if (index != _hoveredIndex)
        {
            _hoveredIndex = index;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hoveredIndex != -1)
        {
            _hoveredIndex = -1;
            Invalidate();
        }
        base.OnMouseLeave(e);
    }

    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
    {
        using (var brush = new SolidBrush(HeaderColor))
            e.Graphics.FillRectangle(brush, e.Bounds);

        using (var pen = new Pen(Palette.Border))
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

        var textBounds = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 20, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? string.Empty, Palette.SmallBold,
            textBounds, Palette.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    protected override void OnDrawItem(DrawListViewItemEventArgs e)
    {
        var bounds = e.Bounds;
        var selected = e.Item?.Selected == true;
        var hovered = e.Item?.Index == _hoveredIndex;

        var background = selected
            ? Palette.AccentSoft
            : hovered
                ? HoverColor
                : e.Item!.Index % 2 == 0
                    ? Palette.Surface
                    : AlternateRowColor;

        using (var brush = new SolidBrush(background))
            e.Graphics.FillRectangle(brush, bounds);

        using (var pen = new Pen(Palette.WithAlpha(Palette.Border, 90)))
            e.Graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
    }

    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
    {
        if (e.Item == null || e.SubItem == null)
            return;

        var bounds = e.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var text = e.SubItem.Text ?? string.Empty;
        var font = e.ColumnIndex == 0 ? Palette.BodyBold : Palette.Body;
        var color = e.Item.Selected
            ? Palette.Text
            : e.Item.ForeColor == Color.Empty
                ? Palette.Text
                : e.Item.ForeColor;

        var textBounds = new Rectangle(bounds.X + 10, bounds.Y, Math.Max(0, bounds.Width - 16), bounds.Height);
        TextRenderer.DrawText(e.Graphics, text, font, textBounds, color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}
