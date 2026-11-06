namespace OrgChat.Core.Enums;

/// <summary>
/// Statut d'un compte utilisateur. IsActive (bool hérité) reste la première
/// barrière ; Status affine avec la suspension temporaire.
/// </summary>
public enum AccountStatus : byte
{
    /// <summary>Compte normal, utilisable.</summary>
    Active = 1,

    /// <summary>Compte temporairement suspendu ; réactivable via
    /// SuspendedUntil (expiré = retourné automatiquement à Active).</summary>
    Suspended = 2,

    /// <summary>Compte désactivé (n'est pas réactivable sans action admin).</summary>
    Disabled = 3,
}
