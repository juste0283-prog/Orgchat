using System.Drawing.Drawing2D;

namespace OrgChat.Client.UI;

/// <summary>Palette et helpers graphiques partagés par tout le client.</summary>
public static class Palette
{
    private static ThemeDefinition Current => ThemeManager.Current;

    public static Color Background => Current.Background;
    public static Color Sidebar => Current.Sidebar;
    public static Color Surface => Current.Surface;
    public static Color SurfaceAlt => Current.SurfaceAlt;
    public static Color SurfaceHover => Current.SurfaceHover;
    public static Color Border => Current.Border;
    public static Color Accent => Current.Accent;
    public static Color AccentHover => Current.AccentHover;
    public static Color AccentSoft => Current.AccentSoft;
    public static Color OnAccent => Current.OnAccent;
    public static Color Text => Current.Text;
    public static Color Muted => Current.Muted;
    public static Color Faint => Current.Faint;
    public static Color Success => Current.Success;
    public static Color Warning => Current.Warning;
    public static Color Danger => Current.Danger;
    public static Color BubbleIn => Current.BubbleIn;
    public static Color HeroStart => Current.HeroStart;
    public static Color HeroEnd => Current.HeroEnd;
    public static Color HeroText => Current.HeroText;
    public static Color HeroMuted => Current.HeroMuted;

    // Barre latérale des espaces d'administration : bleu nuit constant, quel que
    // soit le thème, pour reprendre le rendu des maquettes OrgChat.
    public static readonly Color DashboardSidebar = ColorTranslator.FromHtml("#0F1F36");
    public static readonly Color DashboardSidebarHover = ColorTranslator.FromHtml("#172B49");
    public static readonly Color DashboardSidebarCard = ColorTranslator.FromHtml("#14263F");
    public static readonly Color DashboardSidebarBorder = ColorTranslator.FromHtml("#1D3452");
    public static readonly Color DashboardSidebarText = Color.White;
    public static readonly Color DashboardSidebarMuted = ColorTranslator.FromHtml("#9EAEC5");

    public static readonly Color ChartBlue = ColorTranslator.FromHtml("#1B65FC");
    public static readonly Color ChartTeal = ColorTranslator.FromHtml("#10B981");
    public static readonly Color ChartOrange = ColorTranslator.FromHtml("#F59E0B");
    public static readonly Color ChartPurple = ColorTranslator.FromHtml("#8B5CF6");
    public static readonly Color ChartRed = ColorTranslator.FromHtml("#EF4444");
    public static readonly Color ChartNavy = ColorTranslator.FromHtml("#0F1F36");
    public static readonly Color ChartSlate = ColorTranslator.FromHtml("#64748B");

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
        while (parent is not null)
        {
            if (parent is RoundedPanel rounded)
                return rounded.FillColor;

            if (parent.BackColor != Color.Transparent)
                return parent.BackColor;

            parent = parent.Parent;
        }

        return Background;
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
