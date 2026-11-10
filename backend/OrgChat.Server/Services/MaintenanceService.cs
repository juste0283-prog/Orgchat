using Microsoft.EntityFrameworkCore;
using OrgChat.Infrastructure.Data;
using OrgChat.Server.Services;

namespace OrgChat.Server.Services;

/// <summary>
/// Tâches d'entretien : purge des notifications anciennes et des pièces jointes orphelines.
/// </summary>
public sealed class MaintenanceService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<MaintenanceService> _logger;

    public MaintenanceService(
        IServiceScopeFactory scopeFactory,
        IWebHostEnvironment environment,
        ILogger<MaintenanceService> logger)
    {
        _scopeFactory = scopeFactory;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Laisse le serveur démarrer avant la première passe.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "La tâche d'entretien OrgChat a échoué.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();

        var purgedNotifications = await notifications.PurgeAllReadBeforeAsync(
            DateTime.UtcNow.AddDays(-90));
        if (purgedNotifications > 0)
            _logger.LogInformation("{Count} notification(s) ancienne(s) supprimée(s).", purgedNotifications);

        var expiredAttachments = await db.Attachments
            .Include(a => a.Message)
            .Where(a => a.Message.IsDeleted &&
                        a.Message.DeletedAt != null &&
                        a.Message.DeletedAt < DateTime.UtcNow.AddDays(-7))
            .ToListAsync(ct);

        foreach (var attachment in expiredAttachments)
        {
            TryDeleteUpload(attachment.StoragePath);
            db.Attachments.Remove(attachment);
        }

        if (expiredAttachments.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("{Count} pièce(s) jointe(s) expirée(s) supprimée(s).", expiredAttachments.Count);
        }

        await RemoveOrphanFilesAsync(db, ct);
    }

    private async Task RemoveOrphanFilesAsync(AppDbContext db, CancellationToken ct)
    {
        var uploadsRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "uploads"));
        if (!Directory.Exists(uploadsRoot))
            return;

        var knownPaths = await db.Attachments
            .Select(a => a.StoragePath)
            .ToListAsync(ct);
        var known = knownPaths
            .Select(p => Path.GetFullPath(Path.Combine(
                _environment.ContentRootPath,
                p.Replace('/', Path.DirectorySeparatorChar))))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(uploadsRoot, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            var fullPath = Path.GetFullPath(file);
            if (known.Contains(fullPath))
                continue;

            // On ne supprime que les fichiers assez anciens pour éviter une course avec un upload.
            if (File.GetLastWriteTimeUtc(fullPath) > DateTime.UtcNow.AddHours(-24))
                continue;

            try
            {
                File.Delete(fullPath);
            }
            catch
            {
                // Best-effort : un fichier verrouillé sera retenté à la prochaine passe.
            }
        }
    }

    private void TryDeleteUpload(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            return;

        try
        {
            var uploadsRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "uploads"));
            var fullPath = Path.GetFullPath(Path.Combine(
                _environment.ContentRootPath,
                storagePath.Replace('/', Path.DirectorySeparatorChar)));

            if (!fullPath.StartsWith(uploadsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return;

            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch
        {
            // Best-effort.
        }
    }
}
