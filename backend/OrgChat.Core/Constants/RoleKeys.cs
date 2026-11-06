namespace OrgChat.Core.Constants;

/// <summary>
/// Clés des rôles système du RBAC. Les clés sont les valeurs exposées dans les
/// jetons JWT (claim rôle) et les DTO ; les libellés affichables sont définis
/// par la table Roles (seed).
/// </summary>
public static class RoleKeys
{
    public const string SuperAdmin = "SuperAdmin";
    public const string OrgAdmin = "OrgAdmin";
    public const string Supervisor = "Supervisor";
    public const string DepartmentManager = "DepartmentManager";
    public const string User = "User";

    /// <summary>Ordre de privilège (index supérieur = plus de privilèges).</summary>
    public static readonly string[] Hierarchy =
    [
        User,
        DepartmentManager,
        Supervisor,
        OrgAdmin,
        SuperAdmin,
    ];

    public static readonly IReadOnlySet<string> All = new HashSet<string>(Hierarchy);

    public static bool IsValid(string? key) => key != null && All.Contains(key);

    /// <summary>Rôle système interne utilisé pour les comptes anonymisés supprimés.</summary>
    public static string HighestRole(IEnumerable<string> roles)
    {
        string? best = null;
        foreach (var role in roles)
        {
            if (Array.IndexOf(Hierarchy, role) >= Array.IndexOf(Hierarchy, best ?? User))
                best = role;
        }

        return best ?? User;
    }
}
