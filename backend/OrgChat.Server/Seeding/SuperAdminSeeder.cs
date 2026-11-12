using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Constants;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Entities;
using OrgChat.Core.Enums;
using OrgChat.Infrastructure.Data;

namespace OrgChat.Server.Seeding;

/// <summary>
/// Garantit l'accès au compte SuperAdmin de bootstrap.
/// Les identifiants requis sont toujours : Nom d'utilisateur « superAdmin »
/// et mot de passe « superadmin123! » (le mot de passe est stocké uniquement
/// sous forme de hash PBKDF2 en base).
/// </summary>
public static class SuperAdminSeeder
{
    public const string DefaultDevUsername = "superAdmin";
    public const string DefaultDevPassword = "superadmin123!";

    public static async Task SeedIfNeededAsync(
        IServiceProvider services,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger logger)
    {
        if (environment.IsEnvironment("Test"))
            return;

        const string username = DefaultDevUsername;
        const string password = DefaultDevPassword;
        var email = configuration["OrgChat:SuperAdminEmail"] ?? "superadmin@orgchat.local";

        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var now = DateTime.UtcNow;

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

            var superAdminRoleId = await db.Roles
                .Where(r => r.Key == RoleKeys.SuperAdmin)
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (superAdminRoleId == 0)
            {
                logger.LogWarning("Bootstrap SuperAdmin ignoré : rôle SuperAdmin introuvable.");
                return;
            }

            // Récupère ou crée le compte « superAdmin » (en renommant l'ancien « SuperAdmin » si présent).
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                user = await db.Users.FirstOrDefaultAsync(u => u.Username == "SuperAdmin");
                if (user != null)
                {
                    user.Username = username;
                }
            }

            if (user == null)
            {
                if (await db.Users.AnyAsync(u => u.Email == email))
                {
                    email = $"{username.ToLowerInvariant()}@orgchat.local";
                    if (await db.Users.AnyAsync(u => u.Email == email))
                        email = $"{username.ToLowerInvariant()}+bootstrap@orgchat.local";
                }

                user = new User
                {
                    Username = username,
                    Email = email,
                    DisplayName = "Super Administrateur",
                    FirstName = "Super",
                    LastName = "Administrateur",
                    JobTitle = "Administrateur plateforme",
                    OrganizationId = orgId,
                    CreatedAt = now,
                };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }

            // Garantit les identifiants et l'état du compte.
            user.Username = username;
            user.PasswordHash = hasher.Hash(password);
            user.IsActive = true;
            user.Status = AccountStatus.Active;
            user.SuspendedUntil = null;
            user.SuspensionReason = null;
            user.MustChangePassword = false;
            user.ActivatedAt ??= now;
            if (user.OrganizationId == 0)
                user.OrganizationId = orgId;
            if (string.IsNullOrWhiteSpace(user.Email))
                user.Email = email;

            await db.SaveChangesAsync();

            var hasSuperRole = await db.UserRoles.AnyAsync(ur =>
                ur.UserId == user.Id && ur.RoleId == superAdminRoleId);
            if (!hasSuperRole)
            {
                db.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = superAdminRoleId,
                    OrganizationId = orgId,
                });
                await db.SaveChangesAsync();
            }

            logger.LogInformation(
                "Compte SuperAdmin garanti : {Username} (organisation {OrganizationId}).",
                username, orgId);
        }
        catch (Exception ex)
        {
            // Le bootstrap ne doit jamais empêcher le démarrage.
            logger.LogError(ex, "Échec du bootstrap SuperAdmin (non bloquant).");
        }
    }
}
