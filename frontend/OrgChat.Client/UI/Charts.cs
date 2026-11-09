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
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
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

public sealed class ChartSeries
{
    public ChartSeries(string label, Color color, IEnumerable<ChartPoint> points)
    {
        Label = label;
        Color = color;
        Points = points.ToList();
    }

    public string Label { get; }
    public Color Color { get; }
    public List<ChartPoint> Points { get; }
}

/// <summary>
/// Courbe multi-séries dessinée sans dépendance externe : grille discrète,
/// dégradé sous la première série, points et légende. Utilisée par le
/// tableau de bord pour l'activité des utilisateurs.
/// </summary>
public sealed class LineChartPanel : Control
{
    private List<ChartSeries> _series = new();

    public LineChartPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Palette.Small;
        MinimumSize = new Size(160, 100);
    }

    public string EmptyText { get; set; } = "Aucune donnée sur la période.";

    public IReadOnlyList<ChartSeries> Series => _series;

    public void SetSeries(params ChartSeries[] series)
    {
        _series = series.Where(s => s.Points.Count > 0).ToList();
        Invalidate();
    }

    public void SetSeries(IEnumerable<ChartSeries> series)
    {
        _series = series.Where(s => s.Points.Count > 0).ToList();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Palette.ParentBackground(this));

        if (Width < 30 || Height < 30)
            return;

        if (_series.Count == 0)
        {
            TextRenderer.DrawText(g, Localization.T(EmptyText), Font, ClientRectangle, Palette.Faint,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var count = _series.Max(s => s.Points.Count);
        var maxValue = Math.Max(1d, _series.SelectMany(s => s.Points).Max(p => p.Value));
        var roundedMax = NiceMaximum(maxValue);
        var labels = _series[0].Points;

        const int left = 40;
        const int right = 14;
        const int top = 42;
        const int bottom = 28;
        var plot = new Rectangle(left, top, Math.Max(1, Width - left - right), Math.Max(1, Height - top - bottom));

        using (var gridPen = new Pen(Palette.WithAlpha(Palette.Border, 170), 1f))
        {
            for (var i = 0; i <= 4; i++)
            {
                var y = plot.Bottom - plot.Height * i / 4f;
                g.DrawLine(gridPen, plot.Left, y, plot.Right, y);

                var value = roundedMax * i / 4d;
                var label = value >= 1000 ? $"{value / 1000d:0.#}k" : value.ToString("0");
                TextRenderer.DrawText(g, label, Font,
                    new Rectangle(0, (int)y - 8, left - 7, 16), Palette.Faint,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }

        foreach (var series in _series)
        {
            var points = BuildPoints(series.Points, plot, roundedMax, count);
            if (points.Length == 0)
                continue;

            if (ReferenceEquals(series, _series[0]) && points.Length > 1)
            {
                var area = new PointF[points.Length + 2];
                area[0] = new PointF(points[0].X, plot.Bottom);
                Array.Copy(points, 0, area, 1, points.Length);
                area[^1] = new PointF(points[^1].X, plot.Bottom);

                using var brush = new LinearGradientBrush(
                    new RectangleF(plot.Left, plot.Top, Math.Max(1, plot.Width), Math.Max(1, plot.Height)),
                    Palette.WithAlpha(series.Color, 58),
                    Palette.WithAlpha(series.Color, 4),
                    LinearGradientMode.Vertical);
                g.FillPolygon(brush, area);
            }

            using (var pen = new Pen(series.Color, 2.4f) { LineJoin = LineJoin.Round })
            {
                if (points.Length > 1)
                    g.DrawLines(pen, points);
                else
                    g.DrawLine(pen, points[0].X - 2, points[0].Y, points[0].X + 2, points[0].Y);
            }

            foreach (var point in points)
            {
                using var outer = new SolidBrush(series.Color);
                using var inner = new SolidBrush(Palette.Surface);
                g.FillEllipse(outer, point.X - 3.5f, point.Y - 3.5f, 7f, 7f);
                g.FillEllipse(inner, point.X - 1.6f, point.Y - 1.6f, 3.2f, 3.2f);
            }
        }

        var legendX = left;
        foreach (var series in _series)
        {
            using (var dot = new SolidBrush(series.Color))
                g.FillEllipse(dot, legendX, 14, 8, 8);

            TextRenderer.DrawText(g, series.Label, Font, new Point(legendX + 14, 8), Palette.Muted);
            legendX += 18 + TextRenderer.MeasureText(series.Label, Font).Width;
        }

        var every = Math.Max(1, (int)Math.Ceiling(count / 8f));
        var slot = plot.Width / (float)Math.Max(1, count - 1);
        for (var i = 0; i < labels.Count; i += every)
        {
            if (labels[i].Label.Length == 0)
                continue;

            var x = count <= 1 ? plot.Left + plot.Width / 2f : plot.Left + slot * i;
            var width = 52;
            TextRenderer.DrawText(g, labels[i].Label, Font,
                new Rectangle((int)(x - width / 2f), plot.Bottom + 6, width, 18), Palette.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
        }
    }

    private static PointF[] BuildPoints(IReadOnlyList<ChartPoint> points, Rectangle plot, double max, int count)
    {
        if (points.Count == 0)
            return Array.Empty<PointF>();

        var result = new PointF[points.Count];
        var slot = count <= 1 ? 0f : plot.Width / (float)(count - 1);
        for (var i = 0; i < points.Count; i++)
        {
            var x = count <= 1 ? plot.Left + plot.Width / 2f : plot.Left + slot * i;
            var y = plot.Bottom - (float)(Math.Max(0, points[i].Value) / max) * plot.Height;
            result[i] = new PointF(x, y);
        }
        return result;
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
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
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

        var diameter = Math.Min(Height - 20, Math.Max(70f, (Width - 24) * 0.44f));
        diameter = Math.Max(20f, diameter);
        var ring = new Rectangle(12, (int)((Height - diameter) / 2f), (int)diameter, (int)diameter);
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
                ring.Y + (diameter - size.Height) / 2f - 5);

            using var captionFont = new Font("Segoe UI", 7.5f, FontStyle.Regular);
            using var captionBrush = new SolidBrush(Palette.Muted);
            const string caption = "Total";
            var captionSize = graphics.MeasureString(caption, captionFont);
            graphics.DrawString(caption, captionFont, captionBrush,
                ring.X + (diameter - captionSize.Width) / 2f,
                ring.Y + (diameter + size.Height) / 2f - 8);
        }

        var legendX = ring.Right + 10;
        var legendY = Math.Max(12, (Height - _segments.Count * 24) / 2);
        var legendTextWidth = Math.Max(40, Width - legendX - 12);
        using var legendFont = new Font("Segoe UI", 7.8f);
        foreach (var segment in _segments)
        {
            using var dot = new SolidBrush(segment.Color);
            graphics.FillEllipse(dot, legendX, legendY + 6, 9, 9);

            var percent = total > 0 ? segment.Value / total * 100d : 0;
            var label = $"{Localization.T(segment.Label)} {(int)segment.Value} ({percent:0}%)";
            TextRenderer.DrawText(graphics, label, legendFont,
                new Rectangle(legendX + 15, legendY, legendTextWidth, 20), Palette.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            legendY += 24;
        }
    }
}
