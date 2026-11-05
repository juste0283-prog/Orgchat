using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrgChat.Infrastructure.Data;

namespace OrgChat.Tests;

public class OrgChatTestFactory : WebApplicationFactory<OrgChat.Server.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remplacer SQLite par une base en mémoire pour les tests
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("OrgChatTestDb");
            });
        });

        builder.UseEnvironment("Test");
    }
}
