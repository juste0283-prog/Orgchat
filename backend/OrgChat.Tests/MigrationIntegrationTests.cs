using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrgChat.Core.Constants;
using OrgChat.Infrastructure.Data;
using Xunit;

namespace OrgChat.Tests;

/// <summary>
/// Vérifie que la migration PlatformV2 est réellement non destructive :
/// les comptes existants sont conservés, rattachés à une organisation et
/// convertis vers le RBAC, et le journal d'audit reste inaltérable.
/// </summary>
public class MigrationIntegrationTests
{
    private static DbContextOptions<AppDbContext> SqliteOptions(string path) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

    private static string NewDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"OrgChat.Migration.{Guid.NewGuid():N}.db");

    [Fact]
    public async Task PlatformV2_Migration_Preserves_Existing_Users_And_Maps_Legacy_Admin()
    {
        var dbPath = NewDatabasePath();
        try
        {
            await using var db = new AppDbContext(SqliteOptions(dbPath));

            // 1. Base historique : uniquement InitialCreate.
            var migrator = db.Database.GetService<IMigrator>();
            await migrator.MigrateAsync("20261105000000_InitialCreate");

            // 2. Données métier existantes (ancien schéma : colonne Role 1/2).
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Users (Username, Email, DisplayName, PasswordHash, Role, IsActive, CreatedAt)
                VALUES ('legacy.admin', 'legacy.admin@example.org', 'Legacy Admin', 'PBKDF2-hash', 2, 1, '2026-01-02 03:04:05');
                """);
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Users (Username, Email, DisplayName, PasswordHash, Role, IsActive, CreatedAt)
                VALUES ('legacy.user', 'legacy.user@example.org', 'Legacy User', 'PBKDF2-hash', 1, 1, '2026-01-03 03:04:05');
                """);

            // 3. Application de PlatformV2 sur une base déjà peuplée.
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();

            var admin = await db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .SingleAsync(u => u.Username == "legacy.admin");
            var user = await db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .SingleAsync(u => u.Username == "legacy.user");

            Assert.Equal("Legacy Admin", admin.DisplayName);
            Assert.True(admin.OrganizationId > 0);
            Assert.Contains(admin.UserRoles, ur => ur.Role.Key == RoleKeys.OrgAdmin);

            Assert.Equal("Legacy User", user.DisplayName);
            Assert.True(user.OrganizationId > 0);
            Assert.Contains(user.UserRoles, ur => ur.Role.Key == RoleKeys.User);

            // 4. Les rôles système et l'organisation par défaut ont été créés.
            Assert.Equal(5, await db.Roles.CountAsync());
            Assert.True(await db.Organizations.AnyAsync());

            // 5. Le journal d'audit est protégé par triggers (append-only).
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO AuditLogs (Action, EntityType, Details, Result, CreatedAt)
                VALUES ('TestAction', 'Test', 'audit integrity', 'Success', '2026-01-04 00:00:00');
                """);

            var update = await Assert.ThrowsAnyAsync<Exception>(() =>
                db.Database.ExecuteSqlRawAsync("UPDATE AuditLogs SET Result = 'Denied';"));
            Assert.Contains("écriture seule", update.Message, StringComparison.OrdinalIgnoreCase);

            var delete = await Assert.ThrowsAnyAsync<Exception>(() =>
                db.Database.ExecuteSqlRawAsync("DELETE FROM AuditLogs;"));
            Assert.Contains("suppression", delete.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try
            {
                File.Delete(dbPath);
            }
            catch
            {
                // Fichier encore verrouillé par SQLite : nettoyage non bloquant.
            }
        }
    }
}
