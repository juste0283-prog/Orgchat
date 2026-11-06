using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Constants;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Entities;
using OrgChat.Core.Enums;
using OrgChat.Infrastructure.Data;

namespace OrgChat.Server.Seeding;

/// <summary>
/// Pré-création du compte SuperAdmin de bootstrap.
/// - En <b>Development</b>, crée (une seule fois) un compte `SuperAdmin`
///   avec le mot de passe `superadmin123!` s’il n’existe pas.
///   Il peut ensuite changer ses identifiants via `/api/users/me/password`.
/// - Hors Development (Production), il n’est créé que si
///   `OrgChat:SuperAdminPassword` est explicitement renseigné, et uniquement
///   si aucun SuperAdmin n’existe déjà.
/// - En <b>Test</b>, aucune donnée n’est sémée (base InMemory vierge).
/// </summary>
public static class SuperAdminSeeder
{
    public const string DefaultDevUsername = "SuperAdmin";
    public const string DefaultDevPassword = "superadmin123!";

    public static async Task SeedIfNeededAsync(
        IServiceProvider services,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger logger)
    {
        if (environment.IsEnvironment("Test"))
            return;

        var isDev = environment.IsDevelopment();

        // Surcharge possible par configuration (non commitée en prod).
        var username = configuration["OrgChat:SuperAdminUsername"] ?? DefaultDevUsername;
        var password = configuration["OrgChat:SuperAdminPassword"];
        var email = configuration["OrgChat:SuperAdminEmail"] ?? "superadmin@orgchat.local";

        if (string.IsNullOrWhiteSpace(password))
        {
            if (isDev)
            {
                password = DefaultDevPassword;
            }
            else
            {
                // En production : rien à faire tant qu'aucun mot de passe
                // de bootstrap n'est configuré par l'opérateur.
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(username))
            return;

        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var now = DateTime.UtcNow;

            // Un compte existant avec ce username = déjà bootstrapé.
            var existing = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (existing != null)
            {
                // S'assure qu'il est bien SuperAdmin (si un opérateur l'a rétrogradé,
                // on ne force pas : c'est un choix explicite, on ne toucherait pas).
                var hasSuperRole = await db.UserRoles.AnyAsync(ur =>
                    ur.UserId == existing.Id && ur.Role.Key == RoleKeys.SuperAdmin);
                if (!hasSuperRole)
                {
                    logger.LogInformation(
                        "Le compte bootstrap {Username} existe mais n'est pas SuperAdmin. " +
                        "Le rôle n'est pas réassigné automatiquement.", username);
                }
                return;
            }

            // Hors dev : ne créer que si aucun SuperAdmin n'existe.
            if (!isDev)
            {
                var anySuper = await db.UserRoles.AnyAsync(ur => ur.Role.Key == RoleKeys.SuperAdmin);
                if (anySuper)
                    return;
            }

            var orgId = await db.Organizations
                .Where(o => o.IsActive)
                .OrderBy(o => o.Id)
                .Select(o => o.Id)
                .FirstOrDefaultAsync();

            if (orgId == 0)
            {
                logger.LogWarning("Bootstrap SuperAdmin ignoré : aucune organisation active.");
                return;
            }

            // Évite les collisions avec des comptes démos (email unique).
            if (await db.Users.AnyAsync(u => u.Email == email))
            {
                email = $"{username.ToLowerInvariant()}@orgchat.local";
                if (await db.Users.AnyAsync(u => u.Email == email))
                {
                    logger.LogWarning("Bootstrap SuperAdmin ignoré : e-mail déjà utilisé.");
                    return;
                }
            }

            var user = new User
            {
                Username = username,
                Email = email,
                DisplayName = "Super Administrateur",
                FirstName = "Super",
                LastName = "Administrateur",
                JobTitle = "Administrateur plateforme",
                PasswordHash = hasher.Hash(password),
                IsActive = true,
                Status = AccountStatus.Active,
                OrganizationId = orgId,
                MustChangePassword = false,
                ActivatedAt = now,
                CreatedAt = now,
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = await db.Roles.Where(r => r.Key == RoleKeys.SuperAdmin).Select(r => r.Id).FirstAsync(),
                OrganizationId = orgId,
            });
            await db.SaveChangesAsync();

            logger.LogInformation(
                "Compte SuperAdmin de bootstrap créé : {Username} (organisation {OrganizationId}). " +
                "Changez son mot de passe dès la première connexion.",
                username, orgId);
        }
        catch (Exception ex)
        {
            // Le bootstrap ne doit jamais empêcher le démarrage.
            logger.LogError(ex, "Échec du bootstrap SuperAdmin (non bloquant).");
        }
    }
}
