using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Entities;
using OrgChat.Infrastructure.Data;
using Xunit;

namespace OrgChat.Tests;

public class SqliteDatabaseMaintenanceTests
{
    [Fact]
    public async Task Demo_data_is_idempotent_and_database_verification_passes()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"OrgChat.Seed.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        var databasePath = Path.Combine(directory, "OrgChat.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;

        try
        {
            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync();

            await SqliteDatabaseTestData.SeedDemoDataAsync(db);
            await SqliteDatabaseTestData.SeedDemoDataAsync(db);

            var verification = await SqliteDatabaseVerification.VerifyAsync(db);
            Assert.True(verification.IsValid, verification.ToString());
            Assert.Equal(2, await db.Users.CountAsync(user => user.Username.StartsWith("demo-")));
            Assert.Equal(2, await db.ConversationParticipants.CountAsync());
            Assert.Single(await db.Messages.ToListAsync());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Backup_and_restore_preserve_a_valid_orgchat_database()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"OrgChat.Backup.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        var databasePath = Path.Combine(directory, "OrgChat.db");
        var backupPath = Path.Combine(directory, "backups", "OrgChat.backup.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;

        try
        {
            await using (var db = new AppDbContext(options))
            {
                await db.Database.MigrateAsync();
                db.Users.Add(new User
                {
                    Username = "backup-test",
                    Email = "backup-test@example.org",
                    DisplayName = "Before backup",
                    PasswordHash = "test-only"
                });
                await db.SaveChangesAsync();
            }

            SqliteDatabaseMaintenance.Backup(databasePath, backupPath);

            await using (var db = new AppDbContext(options))
            {
                var user = await db.Users.SingleAsync(u => u.Username == "backup-test");
                user.DisplayName = "After backup";
                await db.SaveChangesAsync();
            }

            SqliteDatabaseMaintenance.RestoreBackup(backupPath, databasePath);

            await using var restoredDb = new AppDbContext(options);
            var restoredUser = await restoredDb.Users.SingleAsync(u => u.Username == "backup-test");
            Assert.Equal("Before backup", restoredUser.DisplayName);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
