using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrgChat.Core.Contracts.Services;
using OrgChat.Infrastructure.Data;
using OrgChat.Infrastructure.Services;
using OrgChat.Server.Hubs;
using OrgChat.Server.Seeding;
using OrgChat.Server.Services;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=OrgChat.db";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var key = builder.Configuration["JwtSettings:Key"] ?? throw new InvalidOperationException("JwtSettings:Key est requis.");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "OrgChat",
            ValidAudience = builder.Configuration["JwtSettings:Audience"] ?? "OrgChat",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = AttachmentService.MaxFileSizeBytes + 2 * 1024 * 1024;
});

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUserRoleService, UserRoleService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<MessageService>();
builder.Services.AddScoped<AttachmentService>();

if (!builder.Environment.IsEnvironment("Test"))
    builder.Services.AddHostedService<MaintenanceService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (dbContext.Database.IsRelational())
    {
        DatabaseBackup.CreateBeforeMigration(connectionString);
        await dbContext.Database.MigrateAsync();
    }
    else
    {
        // Base en mémoire (tests) : crée le schéma et sème les données
        // HasData (Organisation principale, rôles système).
        await dbContext.Database.EnsureCreatedAsync();
    }

    // Pré-création du compte SuperAdmin de bootstrap (Development ou
    // OrgChat:SuperAdminPassword explicite). Ne touche jamais à un compte
    // déjà existant : on ne réinitialise pas son mot de passe.
    var seederLogger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("OrgChat.Server.Seeding.SuperAdminSeeder");
    await SuperAdminSeeder.SeedIfNeededAsync(scope.ServiceProvider, app.Environment, app.Configuration, seederLogger);
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseAuthentication();

// Un jeton JWT reste valide jusqu'à son expiration : on vérifie donc à chaque
// requête que le compte existe, que son statut permet la connexion (suspension
// temporaire, désactivation) et que la session associée (jti) n'est pas révoquée.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdClaim, out var userId))
        {
            var db = context.RequestServices.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            var user = await db.Users
                .AsNoTracking()
                .Select(u => new { u.Id, u.IsActive, u.Status, u.SuspendedUntil, OrgIsActive = u.Organization.IsActive })
                .FirstOrDefaultAsync(u => u.Id == userId);

            var permitted = user != null && user.IsActive && user.OrgIsActive &&
                user.Status switch
                {
                    OrgChat.Core.Enums.AccountStatus.Active => true,
                    OrgChat.Core.Enums.AccountStatus.Suspended => user.SuspendedUntil != null && user.SuspendedUntil <= now,
                    _ => false,
                };

            if (!permitted)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Compte désactivé ou supprimé." });
                return;
            }

            var jti = context.User.FindFirst("jti")?.Value;
            if (!string.IsNullOrEmpty(jti))
            {
                var isRevokedOrExpired = await db.Sessions
                    .AsNoTracking()
                    .AnyAsync(s => s.TokenId == jti &&
                        (s.RevokedAt != null || s.ExpiresAt < now));

                if (isRevokedOrExpired)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { error = "Session expirée ou révoquée." });
                    return;
                }
            }
        }
    }

    await next();
});

app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

app.Run();

public partial class Program { }

/// <summary>
/// Sauvegarde automatique de la base SQLite avant l'application des
/// migrations, garantissant une restauration possible à tout moment.
/// Les sauvegardes sont déposées dans un sous-dossier « backups » à côté
/// du fichier de base ; la purge au-delà de 30 jours est assurée par
/// backend/scripts/backup-database.ps1.
/// </summary>
public static class DatabaseBackup
{
    public static void CreateBeforeMigration(string connectionString)
    {
        try
        {
            var source = ParseDataSource(connectionString);
            if (string.IsNullOrEmpty(source) || !File.Exists(source))
                return;

            var directory = Path.GetDirectoryName(source) ?? ".";
            var backupDirectory = Path.Combine(directory, "backups");
            Directory.CreateDirectory(backupDirectory);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            File.Copy(source, Path.Combine(backupDirectory, $"OrgChat_{timestamp}.db"), overwrite: true);

            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                var side = source + suffix;
                if (File.Exists(side))
                    File.Copy(side, Path.Combine(backupDirectory, $"OrgChat_{timestamp}.db{suffix}"), overwrite: true);
            }
        }
        catch
        {
            // La sauvegarde ne doit jamais empêcher le démarrage :
            // l'échec est silencieux, les migrations restent applicables.
        }
    }

    private static string? ParseDataSource(string connectionString)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var name = part[..separatorIndex].Trim();
            var value = part[(separatorIndex + 1)..].Trim();
            if (name.Equals("Data Source", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("DataSource", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }
}
