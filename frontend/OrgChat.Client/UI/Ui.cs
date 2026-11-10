using OrgChat.Client.Services;

namespace OrgChat.Client.UI;

/// <summary>Fabriques de contrôles cohérents avec le thème sombre du client.</summary>
public static class Ui
{
    public static Label Label(string text, Font? font = null, Color? color = null, bool autoSize = true) => new()
    {
        Text = text,
        Font = font ?? Palette.Body,
        ForeColor = color ?? Palette.Text,
        BackColor = Color.Transparent,
        AutoSize = autoSize,
        UseMnemonic = false
    };

    public static Label Muted(string text, Font? font = null) => Label(text, font ?? Palette.Small, Palette.Muted);

    public static TextBox TextBox(string placeholder = "", bool password = false, bool multiline = false) => new()
    {
        BorderStyle = BorderStyle.None,
        BackColor = Palette.Surface,
        ForeColor = Palette.Text,
        Font = Palette.Body,
        PlaceholderText = placeholder,
        UseSystemPasswordChar = password,
        Multiline = multiline,
        AcceptsReturn = multiline,
        ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None
    };

    /// <summary>Champ arrondi : panneau + zone de saisie centrée.</summary>
    public static RoundedPanel Field(string placeholder, out TextBox textBox, bool password = false,
        bool multiline = false, int height = 44)
    {
        var panel = new RoundedPanel
        {
            Height = height,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border,
            Padding = new Padding(12, 0, 12, 0)
        };

        var box = TextBox(placeholder, password, multiline);
        textBox = box;
        panel.Controls.Add(box);

        if (multiline)
        {
            box.Dock = DockStyle.Fill;
            panel.Padding = new Padding(12, 10, 12, 10);
        }
        else
        {
            box.Location = new Point(12, (height - box.PreferredHeight) / 2);
            box.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            panel.Resize += (_, _) => box.Width = Math.Max(20, panel.Width - 24);
        }

        return panel;
    }

    public static RoundedButton Primary(string text, int width = 160, int height = 42) => new()
    {
        Text = text,
        Width = width,
        Height = height,
        NormalColor = Palette.Accent,
        HoverColor = Palette.AccentHover,
        PressedColor = Palette.Accent,
        CornerRadius = 9
    };

    public static RoundedButton Outline(string text, int width = 130, int height = 38) => new()
    {
        Text = text,
        Width = width,
        Height = height,
        Outline = true,
        ForeColor = Palette.Text,
        CornerRadius = 9
    };

    public static RoundedButton Danger(string text, int width = 130, int height = 38) => new()
    {
        Text = text,
        Width = width,
        Height = height,
        NormalColor = Palette.Danger,
        HoverColor = ColorTranslator.FromHtml("#FF7581"),
        PressedColor = Palette.Danger,
        CornerRadius = 9
    };

    public static RoundedButton Ghost(string text, int width = 38, int height = 38) => new()
    {
        Text = text,
        Width = width,
        Height = height,
        Outline = true,
        ForeColor = Palette.Muted,
        CornerRadius = 8
    };

    public static Label Pill(string text, Color color)
    {
        var label = Label(text, Palette.SmallBold, color);
        label.Padding = new Padding(7, 3, 7, 3);
        label.BackColor = Palette.Blend(Palette.Surface, color, 0.18f);
        return label;
    }

    public static Panel Divider() => new()
    {
        Height = 1,
        Dock = DockStyle.Top,
        BackColor = Palette.Border
    };

    public static void StyleDialog(Form form)
    {
        form.BackColor = Palette.Background;
        form.ForeColor = Palette.Text;
        form.Font = Palette.Body;
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.StartPosition = FormStartPosition.CenterParent;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.ShowIcon = false;
        form.ShowInTaskbar = false;
    }

    public static void StyleForm(Form form)
    {
        form.BackColor = Palette.Background;
        form.ForeColor = Palette.Text;
        form.Font = Palette.Body;
        form.StartPosition = FormStartPosition.CenterScreen;
        form.MinimumSize = new Size(1024, 680);
    }

    public static string FriendlyError(Exception exception) => exception switch
    {
        ApiException api => api.Message,
        OperationCanceledException => "Opération annulée.",
        _ => exception.Message
    };

    public static string FormatConversationTime(DateTime? value)
    {
        if (value is null)
            return string.Empty;

        var local = value.Value.Kind == DateTimeKind.Utc
            ? value.Value.ToLocalTime()
            : value.Value;

        if (local.Date == DateTime.Today)
            return local.ToString("HH:mm");
        if (local.Date == DateTime.Today.AddDays(-1))
            return "Hier";
        if (local.Year == DateTime.Today.Year)
            return local.ToString("dd/MM");
        return local.ToString("dd/MM/yy");
    }

    public static string FormatMessageTime(DateTime value)
    {
        var local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
        return local.ToString("HH:mm");
    }
}
