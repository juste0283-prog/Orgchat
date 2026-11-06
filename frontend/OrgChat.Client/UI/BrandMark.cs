using System.Drawing.Drawing2D;

namespace OrgChat.Client.UI;

/// <summary>Petit logo OrgChat : carré arrondi et bulle de discussion.</summary>
public sealed class BrandMark : Control
{
    public BrandMark()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        Size = new Size(56, 56);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(0, 0, Width - 1, Height - 1);
        using (var gradient = new LinearGradientBrush(rect, Palette.Accent, ColorTranslator.FromHtml("#7A6BF6"), 45f))
        using (var path = Palette.RoundedRect(rect, Width * 0.28f))
            g.FillPath(gradient, path);

        var bubble = new RectangleF(Width * 0.25f, Height * 0.27f, Width * 0.5f, Height * 0.36f);
        using var bubblePath = Palette.RoundedRect(bubble, Width * 0.12f);
        using var white = new SolidBrush(Color.White);
        g.FillPath(white, bubblePath);

        var tail = new[]
        {
            new PointF(bubble.Left + bubble.Width * 0.28f, bubble.Bottom - 1),
            new PointF(bubble.Left + bubble.Width * 0.42f, bubble.Bottom - 1),
            new PointF(bubble.Left + bubble.Width * 0.24f, bubble.Bottom + Height * 0.12f)
        };
        g.FillPolygon(white, tail);

        using var dot = new SolidBrush(Palette.Accent);
        var dotSize = Width * 0.07f;
        var dotY = bubble.Top + (bubble.Height - dotSize) / 2f;
        g.FillEllipse(dot, bubble.Left + bubble.Width * 0.22f, dotY, dotSize, dotSize);
        g.FillEllipse(dot, bubble.Left + bubble.Width * 0.46f, dotY, dotSize, dotSize);
        g.FillEllipse(dot, bubble.Left + bubble.Width * 0.70f, dotY, dotSize, dotSize);
    }
}
