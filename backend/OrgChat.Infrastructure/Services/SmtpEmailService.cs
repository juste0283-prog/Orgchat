using Microsoft.Extensions.Configuration;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Entities;

namespace OrgChat.Infrastructure.Services;

/// <summary>
/// Expéditeur SMTP optionnel. Si SmtpSettings:Host n'est pas renseigné,
/// <see cref="IsConfigured"/> vaut false et aucun envoi n'est effectué :
/// le lien d'activation est uniquement transmis via l'interface admin
/// (copiable). L'application reste 100 % LAN/VPN, sans dépendance cloud.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public SmtpEmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_configuration["SmtpSettings:Host"]);

    public Task SendActivationInvitationAsync(User user, string activationLink)
    {
        if (!IsConfigured)
        {
            return Task.CompletedTask;
        }

        // Point d'extension : brancher ici l'envoi SMTP réel
        // (System.Net.Mail.SmtpClient) quand un serveur SMTP LAN est disponible.
        // Volontairement sans dépendance supplémentaire.
        var from = _configuration["SmtpSettings:From"] ?? "OrgChat";
        var to = user.Email;
        var subject = $"Activation du compte OrgChat — {user.DisplayName}";

        // No-op réel en l'absence de transport : seul le lien copiable est
        // remis via l'UI. Un envoi SMTP production documenterait ici ses
        // paramètres (Host/Port/Username/Password avec TLS).
        _ = (from, to, subject, activationLink);
        return Task.CompletedTask;
    }
}
