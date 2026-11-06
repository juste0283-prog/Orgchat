using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrgChat.Infrastructure.Data;

namespace OrgChat.Tests;

public class OrgChatTestFactory : WebApplicationFactory<global::Program>
{
    private readonly string _databaseName = $"OrgChatTestDb_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remplacer SQLite par une base en mémoire pour les tests
            var descriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("DbContextOptions") == true)
                .ToList();
            foreach (var descriptor in descriptors)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });
        });

        builder.UseEnvironment("Test");
        // L'inscription libre reste accessible uniquement pour l'amorçage des tests.
        builder.UseSetting("OrgChat:EnableSelfRegistration", "true");
    }
}
