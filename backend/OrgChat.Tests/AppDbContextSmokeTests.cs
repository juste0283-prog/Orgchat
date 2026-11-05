using OrgChat.Core.Entities;
using OrgChat.Core.Enums;
using OrgChat.Infrastructure.Data;
using Xunit;

namespace OrgChat.Tests;

public class AppDbContextSmokeTests
{
    [Fact]
    public async Task Initial_migration_creates_schema_and_persists_user()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"OrgChat.Smoke.{Guid.NewGuid()}.db");
        try
        {
            var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync();

            var user = new User
            {
                Username = "alice",
                Email = "alice@example.org",
                DisplayName = "Alice",
                PasswordHash = "PBKDF2-hash",
                Role = UserRole.User,
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            Assert.True(user.Id > 0);

            var found = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == "alice");
            Assert.NotNull(found);
            Assert.Equal("Alice", found.DisplayName);
            Assert.True(found.IsActive);
        }
        finally
        {
            try
            {
                File.Delete(dbPath);
            }
            catch
            {
            }
        }
    }
}
