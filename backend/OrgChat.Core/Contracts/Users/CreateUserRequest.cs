namespace OrgChat.Core.Contracts.Users;

/// <summary>
/// Création d'un compte par un administrateur.
/// Nouveau flux (mission) : renseigner nom, prénom, e-mail pro, département,
/// fonction, rôle et statut ; l'identifiant est généré automatiquement et un
/// jeton d'activation à usage unique est retourné (lien copiable).
/// Les champs historiques (Username/DisplayName/Password) restent acceptés
/// pour compatibilité (tests, import) : si Password est fourni, le compte est
/// créé actif comme avant ; sinon, l'utilisateur définit son mot de passe
/// via le jeton à la première connexion.
/// </summary>
public class CreateUserRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    /// <summary>Organisation cible (SuperAdmin uniquement ; sinon l'organisation courante).</summary>
    public int? OrganizationId { get; set; }

    // --- Compatibilité historique ---
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>Résultat de création : utilisateur + jeton d'activation (une seule fois).</summary>
public class CreatedUserDto : UserDto
{
    /// <summary>Jeton en clair — uniquement dans cette réponse, jamais stocké.</summary>
    public string? ActivationToken { get; set; }
    /// <summary>Lien relatif d'activation (copiable, LAN/VPN).</summary>
    public string? ActivationLink { get; set; }
    public DateTime? InvitationExpiresAt { get; set; }
}
