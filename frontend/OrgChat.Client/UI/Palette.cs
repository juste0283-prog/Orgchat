using System.Drawing.Drawing2D;

namespace OrgChat.Client.UI;

/// <summary>Palette et helpers graphiques partagés par tout le client.</summary>
public static class Palette
{
    public static readonly Color Background = ColorTranslator.FromHtml("#0E1116");
    public static readonly Color Sidebar = ColorTranslator.FromHtml("#12161D");
    public static readonly Color Surface = ColorTranslator.FromHtml("#171C24");
    public static readonly Color SurfaceAlt = ColorTranslator.FromHtml("#1E242E");
    public static readonly Color SurfaceHover = ColorTranslator.FromHtml("#232A36");
    public static readonly Color Border = ColorTranslator.FromHtml("#2A3341");
    public static readonly Color Accent = ColorTranslator.FromHtml("#4F8DFD");
    public static readonly Color AccentHover = ColorTranslator.FromHtml("#6A9FFD");
    public static readonly Color AccentSoft = ColorTranslator.FromHtml("#243B63");
    public static readonly Color Text = ColorTranslator.FromHtml("#E9EEF6");
    public static readonly Color Muted = ColorTranslator.FromHtml("#8C98AA");
    public static readonly Color Faint = ColorTranslator.FromHtml("#637085");
    public static readonly Color Success = ColorTranslator.FromHtml("#3DD68C");
    public static readonly Color Warning = ColorTranslator.FromHtml("#F2B84B");
    public static readonly Color Danger = ColorTranslator.FromHtml("#EF5B68");
    public static readonly Color BubbleIn = ColorTranslator.FromHtml("#1C2330");

    public static readonly Font Body = new("Segoe UI", 9.5f);
    public static readonly Font BodyBold = new("Segoe UI", 9.5f, FontStyle.Bold);
    public static readonly Font Small = new("Segoe UI", 8.25f);
    public static readonly Font SmallBold = new("Segoe UI", 8.25f, FontStyle.Bold);
    public static readonly Font Title = new("Segoe UI", 13f, FontStyle.Bold);
    public static readonly Font Heading = new("Segoe UI", 18f, FontStyle.Bold);
    public static readonly Font Hero = new("Segoe UI", 30f, FontStyle.Bold);

    public static Color WithAlpha(Color color, int alpha) => Color.FromArgb(alpha, color);

    /// <summary>Couleur du fond réellement peint par le parent (utile pour les coins arrondis).</summary>
    public static Color ParentBackground(Control? control)
    {
        var parent = control?.Parent;
        return parent is RoundedPanel rounded
            ? rounded.FillColor
            : parent?.BackColor ?? Background;
    }

    public static Color Blend(Color baseColor, Color overlay, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)(baseColor.R + (overlay.R - baseColor.R) * amount),
            (int)(baseColor.G + (overlay.G - baseColor.G) * amount),
            (int)(baseColor.B + (overlay.B - baseColor.B) * amount));
    }

    public static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1f, Math.Min(radius * 2f, Math.Min(rect.Width, rect.Height)));
        var arc = new RectangleF(rect.X, rect.Y, diameter, diameter);

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.X;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void DrawRounded(Graphics graphics, RectangleF rect, float radius, Color fill, Color? border = null, float borderWidth = 1f)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = RoundedRect(rect, radius);
        using var brush = new SolidBrush(fill);
        graphics.FillPath(brush, path);

        if (border.HasValue)
        {
            using var pen = new Pen(border.Value, borderWidth);
            graphics.DrawPath(pen, path);
        }
    }

    public static Color AvatarColor(string? key)
    {
        Color[] colors =
        {
            ColorTranslator.FromHtml("#4F8DFD"),
            ColorTranslator.FromHtml("#8B6BF6"),
            ColorTranslator.FromHtml("#2DBE9E"),
            ColorTranslator.FromHtml("#E08A46"),
            ColorTranslator.FromHtml("#D95C8A"),
            ColorTranslator.FromHtml("#3FA7C9"),
            ColorTranslator.FromHtml("#C9A227"),
            ColorTranslator.FromHtml("#6D8F4E")
        };

        if (string.IsNullOrWhiteSpace(key))
            return colors[0];

        var sum = key.Aggregate(0, (current, c) => current + c);
        return colors[Math.Abs(sum) % colors.Length];
    }
}
