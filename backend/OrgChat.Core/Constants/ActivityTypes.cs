namespace OrgChat.Core.Constants;

/// <summary>
/// Types d'activités utilisateur persistées (UserActivities.Type).
/// Seules les activités significatives et non personnelles (journalisation de
/// connexion, actions administratives) sont enregistrées : le contenu des
/// conversations n'est jamais journalisé dans le cadre de la supervision.
/// </summary>
public static class ActivityTypes
{
    public const string Login = "Login";
    public const string Logout = "Logout";
    public const string LoginFailed = "LoginFailed";
    public const string AccountActivated = "AccountActivated";
}
