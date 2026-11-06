using OrgChat.Core.Entities;

namespace OrgChat.Core.Contracts.Services;

/// <summary>
/// Service de jetons d'activation et de réinitialisation.
/// Le jeton en clair n'est jamais stocké : seul son hash SHA-256 l'est.
/// </summary>
public interface IInvitationService
{
    /// <summary>Crée une invitation et retourne le jeton en clair (à afficher une seule fois).</summary>
    Task<string> CreateInvitationAsync(
        int userId,
        OrgChat.Core.Enums.InvitationType type,
        int createdByUserId);

    /// <summary>Valide et consomme un jeton. Retourne l'invitation si valide.</summary>
    Task<Invitation?> ValidateAndConsumeAsync(string plainToken, string? ipAddress = null);

    /// <summary>Invalide les invitations actives d'un utilisateur.</summary>
    Task RevokePendingAsync(int userId, OrgChat.Core.Enums.InvitationType? type = null);

    /// <summary>Durée de validité d'une invitation, en heures.</summary>
    int ExpirationHours { get; }
}
