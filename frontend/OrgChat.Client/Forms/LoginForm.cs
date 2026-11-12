using System.Drawing.Drawing2D;
using OrgChat.Client.Configuration;
using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>Écran d'authentification (connexion et activation première connexion).</summary>
public sealed class LoginForm : Form
{
    private readonly ApiClient _api;
    private readonly ClientOptions _options;
    private readonly Action<AuthResponse> _onAuthenticated;
    private readonly Action? _settingsChanged;

    private readonly RoundedPanel _card;
    private readonly Panel _rightPanel;

    private readonly List<Control> _inputs = new();
    private bool _activationMode;
    private bool _busy;
    private string _serverUrl;

    private Label? _errorLabel;
    private RoundedButton? _submitButton;
    private LinkLabel? _switchLink;
    private TextBox? _usernameBox;
    private TextBox? _passwordBox;
    private TextBox? _tokenBox;
    private TextBox? _confirmBox;
    private TextBox? _serverBox;

    public LoginForm(ApiClient api, ClientOptions options, Action<AuthResponse> onAuthenticated,
        Action? settingsChanged = null)
    {
        _api = api;
        _options = options;
        _onAuthenticated = onAuthenticated;
        _settingsChanged = settingsChanged;
        _serverUrl = api.BaseUrl;

        Text = "OrgChat · Connexion";
        ClientSize = new Size(1020, 700);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Palette.Background;
        ForeColor = Palette.Text;
        Font = Palette.Body;
        DoubleBuffered = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Palette.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var hero = BuildHero();
        root.Controls.Add(hero, 0, 0);

        _rightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Palette.Background
        };

        _card = new RoundedPanel
        {
            Width = 430,
            CornerRadius = 18,
            FillColor = Palette.Surface,
            BorderColor = Palette.Border
        };
        _rightPanel.Controls.Add(_card);
        _rightPanel.Resize += (_, _) => CenterCard();
        root.Controls.Add(_rightPanel, 1, 0);

        Controls.Add(root);

        BuildCard();
        CenterCard();
        Localization.Apply(this);

        Shown += (_, _) => (_activationMode ? _tokenBox : _usernameBox)?.Focus();
    }

    private static Panel BuildHero()
    {
        var hero = new HeroPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Palette.Background
        };

        var mark = new BrandMark { Location = new Point(54, 54) };
        hero.Controls.Add(mark);

        var brand = Ui.Label("OrgChat", Palette.Hero, Palette.HeroText, autoSize: false);
        brand.Location = new Point(52, 128);
        brand.Size = new Size(380, 52);
        hero.Controls.Add(brand);

        var tagline = Ui.Label(
            "La messagerie interne de votre organisation, sur le réseau local ou via VPN.",
            new Font("Segoe UI", 11.5f),
            Palette.HeroMuted,
            autoSize: false);
        tagline.Location = new Point(54, 184);
        tagline.Size = new Size(360, 64);
        hero.Controls.Add(tagline);

        var features = new[]
        {
            ("●", "Messages instantanés entre collègues"),
            ("●", "Conversations privées et groupes"),
            ("●", "Historique conservé côté serveur")
        };

        var y = 294;
        foreach (var (dot, text) in features)
        {
            var marker = Ui.Label(dot, Palette.BodyBold, Palette.Accent);
            marker.Location = new Point(54, y);
            hero.Controls.Add(marker);

            var label = Ui.Label(text, Palette.Body, Palette.HeroText);
            label.Location = new Point(76, y);
            hero.Controls.Add(label);

            y += 34;
        }

        var version = Ui.Label("OrgChat v1.0 · LAN / VPN", Palette.Small, Palette.HeroMuted);
        version.Location = new Point(54, 588);
        hero.Controls.Add(version);

        return hero;
    }

    private void CenterCard()
    {
        if (_rightPanel.ClientSize.Width <= 0 || _rightPanel.ClientSize.Height <= 0)
            return;

        _card.Left = Math.Max(16, (_rightPanel.ClientSize.Width - _card.Width) / 2);
        _card.Top = Math.Max(16, (_rightPanel.ClientSize.Height - _card.Height) / 2);
    }

    private void BuildCard()
    {
        _card.SuspendLayout();
        _card.Controls.Clear();
        _inputs.Clear();
        _errorLabel = null;
        _submitButton = null;
        _switchLink = null;

        const int left = 30;
        var width = _card.Width - left * 2;
        var y = 26;

        var title = Ui.Label(_activationMode ? "Première connexion" : "Bienvenue", new Font("Segoe UI", 22f, FontStyle.Bold),
            Palette.Text, autoSize: false);
        title.Location = new Point(left, y);
        title.Size = new Size(width, 38);
        _card.Controls.Add(title);
        y += 36;

        var subtitle = Ui.Label(
            _activationMode
                ? "Définissez votre mot de passe avec le lien d'activation fourni par votre administrateur."
                : "Connectez-vous à votre espace OrgChat.",
            Palette.Body,
            Palette.Muted,
            autoSize: false);
        subtitle.Location = new Point(left, y);
        subtitle.Size = new Size(width, 24);
        _card.Controls.Add(subtitle);
        y += 34;

        if (_activationMode)
        {
            _tokenBox = AddField(left, width, ref y, "Jeton d'activation", "Collez le lien ou le jeton reçu", placeholderSize: 42);
            _passwordBox = AddField(left, width, ref y, "Nouveau mot de passe", "8 caractères minimum", password: true);
            _confirmBox = AddField(left, width, ref y, "Confirmer le mot de passe", "Répétez le mot de passe", password: true);
            _usernameBox = null;
        }
        else
        {
            _tokenBox = null;
            _confirmBox = null;
            _usernameBox = AddField(left, width, ref y, "Nom d'utilisateur", "Votre identifiant");
            _passwordBox = AddField(left, width, ref y, "Mot de passe", "Votre mot de passe", password: true);
        }

        _errorLabel = Ui.Label(string.Empty, Palette.Small, Palette.Danger, autoSize: false);
        _errorLabel.Location = new Point(left, y);
        _errorLabel.Size = new Size(width, 34);
        _card.Controls.Add(_errorLabel);
        y += 34;

        _submitButton = Ui.Primary(_activationMode ? "Activer et se connecter" : "Se connecter", width, 46);
        _submitButton.Location = new Point(left, y);
        _submitButton.Click += OnSubmitClicked;
        _card.Controls.Add(_submitButton);
        AcceptButton = _submitButton;
        y += 58;

        _switchLink = new LinkLabel
        {
            Text = _activationMode ? "Déjà activé ? Se connecter" : "Première connexion ? Activer mon compte",
            Font = Palette.Body,
            LinkColor = Palette.Accent,
            ActiveLinkColor = Palette.AccentHover,
            VisitedLinkColor = Palette.Accent,
            LinkBehavior = LinkBehavior.HoverUnderline,
            BackColor = Color.Transparent,
            Location = new Point(left, y),
            Size = new Size(width, 24),
            TabStop = true
        };
        _switchLink.LinkArea = new LinkArea(0, _switchLink.Text.Length);
        _switchLink.LinkClicked += (_, _) => SwitchMode();
        _card.Controls.Add(_switchLink);
        y += 40;

        var divider = new Panel
        {
            BackColor = Palette.Border,
            Location = new Point(left, y),
            Size = new Size(width, 1)
        };
        _card.Controls.Add(divider);
        y += 16;

        var serverLabel = Ui.Label("Serveur OrgChat", Palette.Small, Palette.Faint);
        serverLabel.Location = new Point(left, y);
        _card.Controls.Add(serverLabel);
        y += 20;

        var serverField = Ui.Field("http://192.168.1.10:5000", out var serverBox, height: 40);
        serverField.Location = new Point(left, y);
        serverField.Width = width;
        serverBox.Text = _serverUrl;
        _serverBox = serverBox;
        _inputs.Add(serverField);
        _card.Controls.Add(serverField);
        y += 52;

        var settingsLink = new LinkLabel
        {
            Text = "Langue et thème",
            Font = Palette.Small,
            LinkColor = Palette.Accent,
            ActiveLinkColor = Palette.AccentHover,
            VisitedLinkColor = Palette.Accent,
            LinkBehavior = LinkBehavior.HoverUnderline,
            BackColor = Color.Transparent,
            Location = new Point(left, y),
            Size = new Size(width, 22),
            TabStop = true
        };
        settingsLink.LinkArea = new LinkArea(0, settingsLink.Text.Length);
        settingsLink.LinkClicked += OnSettingsClicked;
        _card.Controls.Add(settingsLink);
        y += 26;

        _card.Height = y + 18;
        _card.ResumeLayout();
        Localization.Apply(_card);
        CenterCard();
    }

    private TextBox AddField(int left, int width, ref int y, string label, string placeholder, bool password = false, int placeholderSize = 42)
    {
        var caption = Ui.Label(label, Palette.Small, Palette.Muted);
        caption.Location = new Point(left, y);
        _card.Controls.Add(caption);
        y += 20;

        var field = Ui.Field(placeholder, out var box, password, height: 42);
        field.Location = new Point(left, y);
        field.Width = width;
        _inputs.Add(field);
        _card.Controls.Add(field);
        y += 50;

        return box;
    }

    private void SwitchMode()
    {
        if (_busy)
            return;

        _activationMode = !_activationMode;
        BuildCard();
        (_activationMode ? _tokenBox : _usernameBox)?.Focus();
    }

    private void OnSettingsClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        using (var dialog = new SettingsForm())
        {
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
        }

        _settingsChanged?.Invoke();
    }

    private static string ExtractToken(string raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(value))
            return value;

        // Accepte un jeton nu ou un lien complet contenant `?token=...`.
        var tokenIndex = value.IndexOf("token=", StringComparison.OrdinalIgnoreCase);
        if (tokenIndex >= 0)
        {
            var token = value[(tokenIndex + "token=".Length)..];
            token = token.Split('&', '#')[0].Trim();
            return Uri.UnescapeDataString(token);
        }

        if (value.Contains(' ') || value.Contains(Environment.NewLine))
            return value.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? value;

        return value;
    }

    private async void OnSubmitClicked(object? sender, EventArgs e)
    {
        if (_busy)
            return;

        var server = _serverBox?.Text.Trim() ?? _serverUrl;
        if (string.IsNullOrWhiteSpace(server))
        {
            ShowError(Localization.T("Indiquez l'adresse du serveur OrgChat."));
            return;
        }

        if (_activationMode)
        {
            var token = ExtractToken(_tokenBox?.Text ?? string.Empty);
            var password = _passwordBox?.Text ?? string.Empty;
            var confirm = _confirmBox?.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(token))
            {
                ShowError(Localization.T("Le jeton d'activation est requis."));
                _tokenBox?.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError(Localization.T("Le mot de passe est requis."));
                _passwordBox?.Focus();
                return;
            }

            if (password.Length < 8)
            {
                ShowError(Localization.T("Le nouveau mot de passe doit contenir au moins 8 caractères."));
                _passwordBox?.Focus();
                return;
            }

            if (!string.Equals(password, confirm, StringComparison.Ordinal))
            {
                ShowError(Localization.T("Les deux mots de passe ne correspondent pas."));
                _confirmBox?.Focus();
                return;
            }

            await ExecuteAsync(async () =>
            {
                _api.SetBaseUrl(server);
                _serverUrl = _api.BaseUrl;
                var auth = await _api.ActivateAsync(new ActivateAccountRequest
                {
                    Token = token,
                    NewPassword = password
                });
                CompleteAuthentication(auth);
            });

            return;
        }

        var username = _usernameBox?.Text.Trim() ?? string.Empty;
        var loginPassword = _passwordBox?.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError(Localization.T("Le nom d'utilisateur est requis."));
            _usernameBox?.Focus();
            return;
        }

        if (string.IsNullOrEmpty(loginPassword))
        {
            ShowError(Localization.T("Le mot de passe est requis."));
            _passwordBox?.Focus();
            return;
        }

        await ExecuteAsync(async () =>
        {
            _api.SetBaseUrl(server);
            _serverUrl = _api.BaseUrl;
            var auth = await _api.LoginAsync(new LoginRequest
            {
                Username = username,
                Password = loginPassword
            });
            CompleteAuthentication(auth);
        });
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        _busy = true;
        SetBusy(true);
        ShowError(string.Empty);

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ShowError(Ui.FriendlyError(ex));
        }
        finally
        {
            _busy = false;
            SetBusy(false);
        }
    }

    private void CompleteAuthentication(AuthResponse auth)
    {
        _api.SetToken(auth.Token);
        _options.BaseUrl = _api.BaseUrl;
        _onAuthenticated(auth);
    }

    private void SetBusy(bool busy)
    {
        if (_submitButton is not null)
        {
            _submitButton.Enabled = !busy;
            _submitButton.Text = Localization.T(busy
                ? (_activationMode ? "Activation…" : "Connexion…")
                : (_activationMode ? "Activer et se connecter" : "Se connecter"));
        }

        if (_switchLink is not null)
            _switchLink.Enabled = !busy;

        foreach (var input in _inputs)
            input.Enabled = !busy;

        UseWaitCursor = busy;
    }

    private void ShowError(string message)
    {
        if (_errorLabel is null)
            return;

        _errorLabel.Text = message;
        _errorLabel.ForeColor = Palette.Danger;
        _errorLabel.Visible = !string.IsNullOrWhiteSpace(message);
    }

    public void ResetForLogout()
    {
        _activationMode = false;
        _serverUrl = _api.BaseUrl;
        BuildCard();
        ShowError(string.Empty);
        _usernameBox?.Focus();
    }

    /// <summary>Fond dégradé et décorations de la colonne de gauche.</summary>
    private sealed class HeroPanel : Panel
    {
        public HeroPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            var rect = ClientRectangle;

            using (var gradient = new LinearGradientBrush(rect,
                       Palette.HeroStart,
                       Palette.HeroEnd, 55f))
            {
                g.FillRectangle(gradient, rect);
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var pen = new Pen(Color.FromArgb(26, Palette.Accent), 1.4f))
            {
                g.DrawEllipse(pen, Width - 190, -140, 360, 360);
                g.DrawEllipse(pen, Width - 120, 120, 280, 280);
            }

            using var dot = new SolidBrush(Palette.WithAlpha(Palette.HeroText, 18));
            for (var x = 30; x < Width; x += 34)
            {
                for (var y = 30; y < Height; y += 34)
                    g.FillEllipse(dot, x, y, 1.6f, 1.6f);
            }

            using var glow = new SolidBrush(Color.FromArgb(24, Palette.Accent));
            g.FillEllipse(glow, -120, Height - 260, 340, 340);
        }
    }
}
