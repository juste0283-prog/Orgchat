using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Contracts.Messages;
using OrgChat.Core.Entities;
using OrgChat.Infrastructure.Data;

namespace OrgChat.Server.Services;

public sealed record AttachmentDownload(Attachment Attachment, string FullPath);

/// <summary>Validation, stockage disque et accès sécurisé aux pièces jointes.</summary>
public class AttachmentService
{
    public const long MaxFileSizeBytes = 20 * 1024 * 1024;
    public const long MaxConversationStorageBytes = 500 * 1024 * 1024;

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bat", ".cmd", ".com", ".exe", ".msi", ".ps1", ".scr", ".vbs", ".vbe",
        ".wsf", ".wsh", ".js", ".jse", ".dll", ".sh", ".app", ".cpl", ".hta"
    };

    private readonly AppDbContext _db;
    private readonly MessageService _messages;
    private readonly IWebHostEnvironment _environment;

    public AttachmentService(AppDbContext db, MessageService messages, IWebHostEnvironment environment)
    {
        _db = db;
        _messages = messages;
        _environment = environment;
    }

    public async Task<MessageDto> SendAttachmentAsync(
        int conversationId,
        int senderId,
        IFormFile file,
        string? content)
    {
        if (file is null || file.Length <= 0)
            throw new InvalidOperationException("Le fichier est vide ou introuvable.");

        if (file.Length > MaxFileSizeBytes)
            throw new InvalidOperationException("La pièce jointe dépasse la taille maximale de 20 Mo.");

        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == senderId);
        if (!isParticipant)
            throw new InvalidOperationException("Vous ne faites pas partie de cette conversation.");

        var usedBytes = await _db.Attachments
            .Where(a => a.Message.ConversationId == conversationId && !a.Message.IsDeleted)
            .SumAsync(a => (long?)a.SizeInBytes) ?? 0;

        if (usedBytes + file.Length > MaxConversationStorageBytes)
            throw new InvalidOperationException("Le quota de pièces jointes de cette conversation est atteint.");

        var originalFileName = Path.GetFileName(file.FileName).Trim();
        if (originalFileName.Length == 0)
            originalFileName = "fichier";

        if (originalFileName.Length > 500)
        {
            var fileExtension = Path.GetExtension(originalFileName);
            var baseNameLength = Math.Max(1, 500 - fileExtension.Length);
            originalFileName = originalFileName[..baseNameLength] + fileExtension;
        }

        var extension = Path.GetExtension(originalFileName);
        if (extension.Length > 20)
            extension = string.Empty;

        if (BlockedExtensions.Contains(extension))
            throw new InvalidOperationException("Ce type de fichier n'est pas autorisé.");

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType.Trim();
        if (contentType.Length > 100)
            contentType = "application/octet-stream";

        var now = DateTime.UtcNow;
        var relativePath = Path.Combine(
            "uploads",
            now.ToString("yyyy"),
            now.ToString("MM"),
            $"{Guid.NewGuid():N}{extension}").Replace('\\', '/');

        var fullPath = Path.Combine(
            _environment.ContentRootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream);
        }

        var attachment = new Attachment
        {
            OriginalFileName = originalFileName,
            StorageFileName = Path.GetFileName(relativePath),
            ContentType = contentType,
            SizeInBytes = file.Length,
            StoragePath = relativePath
        };

        try
        {
            return await _messages.SendAttachmentMessageAsync(conversationId, senderId, content, attachment);
        }
        catch
        {
            TryDelete(fullPath);
            throw;
        }
    }

    public async Task<AttachmentDownload?> GetDownloadAsync(int attachmentId, int userId)
    {
        var attachment = await _db.Attachments
            .AsNoTracking()
            .Include(a => a.Message)
            .FirstOrDefaultAsync(a => a.Id == attachmentId);

        if (attachment is null || attachment.Message.IsDeleted)
            return null;

        var canDownload = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == attachment.Message.ConversationId && p.UserId == userId);

        if (!canDownload)
            return null;

        var uploadsRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "uploads"));
        var fullPath = Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath,
            attachment.StoragePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(uploadsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return null;

        return File.Exists(fullPath) ? new AttachmentDownload(attachment, fullPath) : null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Nettoyage best-effort après un échec de persistance.
        }
    }
}
