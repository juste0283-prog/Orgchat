using OrgChat.Core.Entities;

namespace OrgChat.Core.Contracts.Services;

/// <summary>
/// Envoi d'e-mail optionnel (SMTP). En LAN/VPN sans SMTP, l'application
/// fonctionne exclusivement par liens copiables remis par l'administrateur.
/// </summary>
public interface IEmailService
{
    /// <summary>Indique si un expéditeur SMTP est configuré.</summary>
    bool IsConfigured { get; }

    /// <summary>Envoie l'invitation d'activation à l'utilisateur. Best-effort.</summary>
    Task SendActivationInvitationAsync(User user, string activationLink);
}
