using System.Drawing.Drawing2D;
using OrgChat.Client.Services;

namespace OrgChat.Client.UI;

public sealed class ChartPoint
{
    public ChartPoint(string label, double value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }
    public double Value { get; }
}

public sealed class ChartSegment
{
    public ChartSegment(string label, double value, Color color)
    {
        Label = label;
        Value = value;
        Color = color;
    }

    public string Label { get; }
    public double Value { get; }
    public Color Color { get; }
}

/// <summary>
/// Histogramme dessiné (aucune dépendance externe) : grille discrète, barres
/// arrondies et libellés d'axe cohérents avec le thème actif.
/// </summary>
public sealed class BarChartPanel : Control
{
    private List<ChartPoint> _points = new();
    private Color? _barColor;

    public BarChartPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Font = Palette.Small;
        MinimumSize = new Size(120, 80);
    }

    public Color BarColor
    {
        get => _barColor ?? Palette.Accent;
        set { _barColor = value; Invalidate(); }
    }

    public string EmptyText { get; set; } = "Aucune donnée sur la période.";

    public IReadOnlyList<ChartPoint> Points => _points;

    public void SetPoints(IEnumerable<ChartPoint> points)
    {
        _points = points.ToList();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Palette.ParentBackground(this));

        var bounds = new Rectangle(0, 0, Width, Height);
        if (bounds.Width < 20 || bounds.Height < 20)
            return;

        var left = 34;
        var right = 10;
        var top = 14;
        var bottom = 30;
        var plot = new Rectangle(left, top, Math.Max(1, bounds.Width - left - right), Math.Max(1, bounds.Height - top - bottom));

        if (_points.Count == 0)
        {
            TextRenderer.DrawText(graphics, Localization.T(EmptyText), Font,
                plot, Palette.Faint, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var max = Math.Max(1d, _points.Max(p => p.Value));
        var roundedMax = NiceMaximum(max);

        using (var gridPen = new Pen(Palette.WithAlpha(Palette.Border, 150), 1f))
        {
            for (var i = 0; i <= 4; i++)
            {
                var y = plot.Bottom - plot.Height * i / 4f;
                graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);

                var value = roundedMax * i / 4d;
                var label = value >= 1000 ? $"{value / 1000d:0.#}k" : value.ToString("0");
                TextRenderer.DrawText(graphics, label, Font,
                    new Rectangle(0, (int)y - 8, left - 6, 16), Palette.Faint,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }

        var count = _points.Count;
        var slot = plot.Width / (float)count;
        var barWidth = Math.Max(3f, Math.Min(30f, slot * 0.55f));
        var labelEvery = Math.Max(1, (int)Math.Ceiling(count / 8f));

        for (var i = 0; i < count; i++)
        {
            var point = _points[i];
            var height = (float)(Math.Max(0, point.Value) / roundedMax) * plot.Height;
            var x = plot.Left + slot * i + (slot - barWidth) / 2f;
            var rect = new RectangleF(x, plot.Bottom - height, barWidth, Math.Max(1.5f, height));

            using var path = Palette.RoundedRect(rect, Math.Min(6f, barWidth / 2f));
            using var brush = new LinearGradientBrush(
                new RectangleF(rect.X, rect.Y - 1, Math.Max(1f, rect.Width), Math.Max(2f, rect.Height + 2)),
                Palette.Blend(BarColor, Color.White, 0.18f),
                BarColor,
                LinearGradientMode.Vertical);
            graphics.FillPath(brush, path);

            if (i % labelEvery == 0)
            {
                TextRenderer.DrawText(graphics, point.Label, Font,
                    new Rectangle((int)(plot.Left + slot * i), plot.Bottom + 6, (int)Math.Ceiling(slot * labelEvery), 18),
                    Palette.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
            }
        }
    }

    private static double NiceMaximum(double value)
    {
        if (value <= 4)
            return 4;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        var normalized = value / magnitude;
        var step = normalized switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            _ => 10,
        };
        return Math.Max(1, step * magnitude);
    }
}

/// <summary>Anneau de répartition avec légende intégrée.</summary>
public sealed class DonutChartPanel : Control
{
    private List<ChartSegment> _segments = new();

    public DonutChartPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Font = Palette.Small;
    }

    public void SetSegments(IEnumerable<ChartSegment> segments)
    {
        _segments = segments.ToList();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Palette.ParentBackground(this));

        var total = _segments.Sum(s => s.Value);
        if (total <= 0)
        {
            TextRenderer.DrawText(graphics, Localization.T("Aucune donnée."), Font,
                ClientRectangle, Palette.Faint,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var legendWidth = Math.Min(Width / 2, 170);
        var diameter = Math.Min(Height - 20, Width - legendWidth - 24);
        diameter = Math.Max(20, diameter);
        var ring = new Rectangle(12, (Height - diameter) / 2, diameter, diameter);
        var inner = Rectangle.Inflate(ring, -(int)(diameter * 0.16f), -(int)(diameter * 0.16f));

        var start = -90f;
        foreach (var segment in _segments.Where(s => s.Value > 0))
        {
            var sweep = (float)(segment.Value / total * 360d);
            using var brush = new SolidBrush(segment.Color);
            graphics.FillPie(brush, ring, start, sweep);
            start += sweep;
        }

        using (var hole = new SolidBrush(Palette.ParentBackground(this)))
            graphics.FillEllipse(hole, inner);

        using (var textBrush = new SolidBrush(Palette.Text))
        {
            using var countFont = new Font("Segoe UI", Math.Max(12f, diameter / 8f), FontStyle.Bold);
            var totalText = ((int)total).ToString();
            var size = graphics.MeasureString(totalText, countFont);
            graphics.DrawString(totalText, countFont, textBrush,
                ring.X + (diameter - size.Width) / 2f,
                ring.Y + (diameter - size.Height) / 2f);
        }

        var legendX = ring.Right + 18;
        var legendY = Math.Max(12, (Height - _segments.Count * 26) / 2);
        foreach (var segment in _segments)
        {
            using var dot = new SolidBrush(segment.Color);
            graphics.FillEllipse(dot, legendX, legendY + 6, 9, 9);

            var percent = total > 0 ? segment.Value / total * 100d : 0;
            var label = $"{Localization.T(segment.Label)}  {(int)segment.Value} ({percent:0}%)";
            TextRenderer.DrawText(graphics, label, Font,
                new Point(legendX + 16, legendY), Palette.Text);
            legendY += 26;
        }
    }
}
