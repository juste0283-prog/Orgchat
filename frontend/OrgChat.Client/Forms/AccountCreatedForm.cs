using OrgChat.Client.Models;
using OrgChat.Client.Services;
using OrgChat.Client.UI;

namespace OrgChat.Client.Forms;

/// <summary>
/// Confirmation de création de compte. Affiche clairement l'identifiant et,
/// lorsque le compte attend une activation, le lien à transmettre à l'utilisateur.
/// </summary>
public sealed class AccountCreatedForm : Form
{
    private readonly string _activationLink;
    private readonly RoundedButton _copyButton;

    public AccountCreatedForm(CreatedUserDto user, string? activationLink, bool directPassword)
    {
        _activationLink = activationLink ?? string.Empty;

        Ui.StyleDialog(this);
        ClientSize = new Size(600, directPassword ? 392 : 470);
        Text = "Compte créé";

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(26),
            BackColor = Palette.Background,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, directPassword ? 70 : 132));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var check = Ui.Label("✓", new Font("Segoe UI", 25f, FontStyle.Bold),
            Palette.Success, autoSize: false);
        check.SetBounds(0, 0, 38, 38);
        check.TextAlign = ContentAlignment.MiddleCenter;
        var title = Ui.Label("Compte créé", new Font("Segoe UI", 17f, FontStyle.Bold));
        title.Location = new Point(48, 0);
        var subtitle = Ui.Label(
            directPassword
                ? "Le compte est prêt à être utilisé."
                : "Le compte est en attente d'activation.",
            Palette.Body,
            Palette.Muted);
        subtitle.Location = new Point(49, 34);
        header.Controls.Add(check);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        root.Controls.Add(header, 0, 0);

        var identity = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };
        var identityCaption = Ui.Label("Identifiant", Palette.Small, Palette.Muted);
        identityCaption.Location = new Point(0, 0);
        var identityValue = Ui.Label("@" + user.Username, Palette.BodyBold, Palette.Text);
        identityValue.Location = new Point(0, 19);
        var nameValue = Ui.Label(user.DisplayName, Palette.Small, Palette.Muted);
        nameValue.Location = new Point(0, 39);
        identity.Controls.Add(identityCaption);
        identity.Controls.Add(identityValue);
        identity.Controls.Add(nameValue);
        root.Controls.Add(identity, 0, 1);

        var linkArea = new Panel { Dock = DockStyle.Fill, BackColor = Palette.Background };

        var linkCaption = Ui.Label("Lien d'activation (usage unique)", Palette.Small, Palette.Muted);
        linkCaption.Location = new Point(0, 0);
        var linkField = Ui.Field("", out var linkBox, height: 40);
        linkBox.ReadOnly = true;
        linkBox.Text = _activationLink;
        linkField.Location = new Point(0, 20);
        linkField.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        linkArea.Resize += (_, _) => linkField.Width = Math.Max(100, linkArea.Width);

        var expiry = Ui.Label(string.Empty, Palette.Small, Palette.Faint, autoSize: false);
        expiry.SetBounds(0, 68, 548, 20);
        if (user.InvitationExpiresAt != null)
        {
            expiry.Text = Localization.F("Valable jusqu'au {0}.",
                user.InvitationExpiresAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
        }

        var directMessage = Ui.Label(
            "Aucun lien n'est nécessaire : le mot de passe a été défini lors de la création.",
            Palette.Body,
            Palette.Muted,
            autoSize: false);
        directMessage.SetBounds(0, 0, 548, 54);
        directMessage.TextAlign = ContentAlignment.MiddleLeft;
        directMessage.Visible = directPassword;

        linkCaption.Visible = !directPassword;
        linkField.Visible = !directPassword;
        expiry.Visible = !directPassword;

        linkArea.Controls.Add(linkCaption);
        linkArea.Controls.Add(linkField);
        linkArea.Controls.Add(expiry);
        linkArea.Controls.Add(directMessage);
        root.Controls.Add(linkArea, 0, 2);

        var instructions = Ui.Label(
            directPassword
                ? "Communiquez l'identifiant et le mot de passe à l'utilisateur par un canal sécurisé. Il pourra se connecter dès maintenant."
                : "Copiez ce lien et transmettez-le à l'utilisateur. Il pourra l'ouvrir dans son navigateur ou le coller dans OrgChat, puis choisir son mot de passe.",
            Palette.Body,
            Palette.Muted,
            autoSize: false);
        instructions.Dock = DockStyle.Fill;
        instructions.Padding = new Padding(0, 8, 0, 0);
        root.Controls.Add(instructions, 0, 3);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Palette.Background,
            Padding = new Padding(0, 8, 0, 0),
        };

        var close = Ui.Outline("Fermer", 110, 40);
        close.Click += (_, _) => Close();
        footer.Controls.Add(close);

        _copyButton = Ui.Primary("Copier le lien", 170, 40);
        _copyButton.Margin = new Padding(0, 0, 10, 0);
        _copyButton.Visible = !directPassword;
        _copyButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_activationLink))
                return;

            Clipboard.SetText(_activationLink);
            _copyButton.Text = Localization.T("Copié");
        };
        footer.Controls.Add(_copyButton);
        root.Controls.Add(footer, 0, 4);

        Controls.Add(root);
        AcceptButton = close;
        CancelButton = close;
        Localization.Apply(this);
    }
}
