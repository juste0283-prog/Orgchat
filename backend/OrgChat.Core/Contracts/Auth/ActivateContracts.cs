using OrgChat.Core.Enums;

namespace OrgChat.Core.Contracts.Auth;

/// <summary>Définition du mot de passe à la première connexion ou réinitialisation.</summary>
public class ActivateAccountRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>Validation sans consommation d'un jeton (optionnel, pour l'UI).</summary>
public class ValidateInvitationRequest
{
    public string Token { get; set; } = string.Empty;
}

public class ValidateInvitationResponse
{
    public bool IsValid { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Error { get; set; }
}
