using System.Drawing.Drawing2D;

namespace OrgChat.Client.UI;

/// <summary>Panneau à coins arrondis, dessiné sans bordure native.</summary>
public class RoundedPanel : Panel
{
    private int _cornerRadius = 12;

    public RoundedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
        BackColor = Palette.Background;
    }

    public int CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = Math.Max(0, value); Invalidate(); }
    }

    public Color FillColor { get; set; } = Palette.Surface;

    public Color BorderColor { get; set; } = Palette.Border;

    public bool ShowBorder { get; set; } = true;

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Palette.ParentBackground(this));

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        Palette.DrawRounded(e.Graphics, rect, CornerRadius, FillColor, ShowBorder ? BorderColor : null);
    }
}

/// <summary>Panneau défilant qui ne clignote pas (liste des conversations).</summary>
public sealed class SmoothFlowPanel : FlowLayoutPanel
{
    public SmoothFlowPanel()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        DoubleBuffered = true;
    }
}
