namespace OrgChat.Core.Enums;

public enum InvitationType : byte
{
    /// <summary>Lien d'activation de compte (définition du mot de passe initial).</summary>
    AccountActivation = 1,

    /// <summary>Lien de réinitialisation de mot de passe envoyé par un admin.</summary>
    PasswordReset = 2,
}
