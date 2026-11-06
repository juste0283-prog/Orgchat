using OrgChat.Client.Models;

namespace OrgChat.Client.UI;

/// <summary>Ligne sélectionnable d'un utilisateur (création de conversation).</summary>
public sealed class UserSelectRow : Control
{
    private bool _hovered;
    private bool _checked;

    public UserSelectRow(UserDto user, bool singleSelect)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        User = user;
        SingleSelect = singleSelect;
        Height = 58;
        Cursor = Cursors.Hand;
        BackColor = Palette.Background;
        Margin = Padding.Empty;
    }

    public UserDto User { get; }

    public bool SingleSelect { get; }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
                return;
            _checked = value;
            Invalidate();
        }
    }

    public event EventHandler? CheckedChanged;

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
        CheckedChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var background = _checked
            ? Palette.WithAlpha(Palette.Accent, 34)
            : _hovered
                ? Palette.SurfaceHover
                : Palette.Background;

        Palette.DrawRounded(g, new RectangleF(0, 1, Width - 1, Height - 3), 8, background);

        var indicator = new Rectangle(14, (Height - 18) / 2, 18, 18);
        if (SingleSelect)
        {
            using var pen = new Pen(_checked ? Palette.Accent : Palette.Border, 2f);
            g.DrawEllipse(pen, indicator);
            if (_checked)
            {
                using var fill = new SolidBrush(Palette.Accent);
                var inner = indicator;
                inner.Inflate(-5, -5);
                g.FillEllipse(fill, inner);
            }
        }
        else
        {
            Palette.DrawRounded(g, indicator, 5,
                _checked ? Palette.Accent : Palette.Surface,
                _checked ? Palette.Accent : Palette.Border);

            if (_checked)
            {
                using var pen = new Pen(Color.White, 2f);
                g.DrawLines(pen, new[]
                {
                    new PointF(indicator.X + 4, indicator.Y + 9),
                    new PointF(indicator.X + 8, indicator.Y + 13),
                    new PointF(indicator.X + 14, indicator.Y + 5)
                });
            }
        }

        AvatarControl.DrawAvatar(g, new Rectangle(44, 10, 36, 36), User.DisplayName, User.Id.ToString());

        TextRenderer.DrawText(g, string.IsNullOrWhiteSpace(User.DisplayName) ? User.Username : User.DisplayName,
            Palette.BodyBold, new Rectangle(92, 10, Math.Max(40, Width - 120), 18), Palette.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        var subtitle = User.IsAdmin ? $"@{User.Username} · Administrateur" : $"@{User.Username}";
        TextRenderer.DrawText(g, subtitle, Palette.Small,
            new Rectangle(92, 30, Math.Max(40, Width - 120), 16),
            User.IsAdmin ? Palette.Warning : Palette.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
