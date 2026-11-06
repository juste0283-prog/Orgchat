using OrgChat.Core.Constants;
using System.Security.Claims;

namespace OrgChat.Server.Controllers;

/// <summary>
/// Helpers d'isolation multi-organisations : le périmètre d'accès d'un
/// utilisateur est son organisation d'appartenance (claim « org ») ;
/// seul le SuperAdmin est toujours global. Toutes les règles métier
/// s'appuient sur ces fonctions côté serveur.
/// </summary>
public static class OrgScope
{
    public static int GetUserOrganizationId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst("org")?.Value;
        return int.TryParse(claim, out var organizationId) ? organizationId : 0;
    }

    public static bool IsSuperAdmin(this ClaimsPrincipal user) =>
        user.IsInRole(RoleKeys.SuperAdmin);

    public static bool IsOrgAdmin(this ClaimsPrincipal user) =>
        user.IsInRole(RoleKeys.OrgAdmin);

    public static bool IsSupervisor(this ClaimsPrincipal user) =>
        user.IsInRole(RoleKeys.Supervisor);

    public static bool IsDepartmentManager(this ClaimsPrincipal user) =>
        user.IsInRole(RoleKeys.DepartmentManager);

    public static bool IsOrgAdminHere(this ClaimsPrincipal user) =>
        user.IsOrgAdmin() || user.IsSuperAdmin();

    /// <summary>Vérifie que l'entité vue/infligée appartient bien à l'organisation
    /// courante (sauf SuperAdmin, global). Renvoie true si autorisé.</summary>
    public static bool BelongsToOrganization(this ClaimsPrincipal user, int entityOrganizationId)
    {
        if (user.IsSuperAdmin())
            return true;
        return entityOrganizationId == user.GetUserOrganizationId() && entityOrganizationId != 0;
    }
}
