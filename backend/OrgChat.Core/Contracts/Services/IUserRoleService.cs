using System.Threading.Tasks;

namespace OrgChat.Core.Contracts.Services;

public interface IUserRoleService
{
    /// <summary>Charge et retourne l'utilisateur avec ses rôles (ou null).</summary>
    Task<OrgChat.Core.Entities.User?> GetUserWithRolesAsync(int userId);

    /// <summary>Rôle principal (le plus privilégié) d'un utilisateur chargé avec ses rôles.</summary>
    string GetPrimaryRole(OrgChat.Core.Entities.User user);

    /// <summary>Affecte un rôle à l'échelle d'une organisation (remplace le rôle organisationnel précédent) et enregistre la modification.</summary>
    Task SetOrganizationRoleAsync(OrgChat.Core.Entities.User user, string roleKey);

    /// <summary>Affecte le rôle Utilisateur de l'organisation (auto-inscription, tests).</summary>
    Task AssignDefaultRoleAsync(OrgChat.Core.Entities.User user);
}
