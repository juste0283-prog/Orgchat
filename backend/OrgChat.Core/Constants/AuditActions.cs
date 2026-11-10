namespace OrgChat.Core.Constants;

/// <summary>
/// Types d'actions enregistrées dans le journal d'audit (AuditLogs.Action).
/// </summary>
public static class AuditActions
{
    // Comptes
    public const string AccountCreated = "AccountCreated";
    public const string AccountActivated = "AccountActivated";
    public const string AccountUpdated = "AccountUpdated";
    public const string AccountDeactivated = "AccountDeactivated";
    public const string AccountReactivated = "AccountReactivated";
    public const string AccountSuspended = "AccountSuspended";
    public const string AccountUnsuspended = "AccountUnsuspended";
    public const string AccountDeleted = "AccountDeleted";
    public const string PasswordReset = "PasswordReset";

    // Rôles et permissions
    public const string RoleChanged = "RoleChanged";

    // Organisations / départements
    public const string OrganizationCreated = "OrganizationCreated";
    public const string OrganizationUpdated = "OrganizationUpdated";
    public const string DepartmentCreated = "DepartmentCreated";
    public const string DepartmentUpdated = "DepartmentUpdated";
    public const string DepartmentDeactivated = "DepartmentDeactivated";
    public const string DepartmentMemberAdded = "DepartmentMemberAdded";
    public const string DepartmentMemberRemoved = "DepartmentMemberRemoved";

    // Authentification et sécurité
    public const string LoggedIn = "LoggedIn";
    public const string LoggedOut = "LoggedOut";
    public const string LoginFailed = "LoginFailed";
    public const string SessionRevoked = "SessionRevoked";
    public const string AccessDenied = "AccessDenied";
}
