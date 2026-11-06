using System.Drawing.Drawing2D;

namespace OrgChat.Client.UI;

/// <summary>Pastille d'avatar avec initiales et indicateur de présence.</summary>
public sealed class AvatarControl : Control
{
    private bool _online;

    public AvatarControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        Size = new Size(42, 42);
        BackColor = Color.Transparent;
    }

    public string DisplayName { get; set; } = string.Empty;

    public string Seed { get; set; } = string.Empty;

    public bool ShowStatus { get; set; }

    public bool Online
    {
        get => _online;
        set { _online = value; Invalidate(); }
    }

    public string Initials => UserInitials(DisplayName, Seed);

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        DrawAvatar(e.Graphics, ClientRectangle, DisplayName, Seed, ShowStatus, Online);
    }

    public static void DrawAvatar(Graphics graphics, Rectangle bounds, string? displayName, string? seed,
        bool showStatus = false, bool online = false)
    {
        var size = Math.Min(bounds.Width, bounds.Height);
        var rect = new RectangleF(
            bounds.X + (bounds.Width - size) / 2f,
            bounds.Y + (bounds.Height - size) / 2f,
            size,
            size);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using (var brush = new SolidBrush(Palette.AvatarColor(seed ?? displayName)))
            graphics.FillEllipse(brush, rect);

        var initials = UserInitials(displayName, seed);
        using var font = new Font("Segoe UI", Math.Max(7.5f, size * 0.32f), FontStyle.Bold);
        TextRenderer.DrawText(
            graphics,
            initials,
            font,
            Rectangle.Round(rect),
            Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        if (showStatus)
        {
            var dotSize = Math.Max(8f, size * 0.24f);
            var dot = new RectangleF(rect.Right - dotSize - 1, rect.Bottom - dotSize - 1, dotSize, dotSize);
            using var border = new SolidBrush(Palette.Sidebar);
            graphics.FillEllipse(border, dot);
            dot.Inflate(-1.2f, -1.2f);
            using var fill = new SolidBrush(online ? Palette.Success : Palette.Faint);
            graphics.FillEllipse(fill, dot);
        }
    }

    public static string UserInitials(string? displayName, string? fallback)
    {
        var value = string.IsNullOrWhiteSpace(displayName) ? fallback : displayName;
        if (string.IsNullOrWhiteSpace(value))
            return "?";

        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[1][0]));

        return value.Length >= 2 ? value[..2].ToUpperInvariant() : value.ToUpperInvariant();
    }
}
