using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OrgChat.Client.Models;

namespace OrgChat.Client.Services;

public sealed record SavedSession(string BaseUrl, AuthResponse Auth);

/// <summary>
/// Conserve la session entre deux lancements, chiffrée avec DPAPI pour l'utilisateur Windows courant.
/// </summary>
public static class SessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OrgChat",
        "session.dat");

    public static async Task SaveAsync(string baseUrl, AuthResponse auth, CancellationToken ct = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(new SavedSession(baseUrl, auth), JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(FilePath, protectedBytes, ct);
        }
        catch
        {
            // La persistance de session est un confort : son échec ne bloque pas la connexion.
        }
    }

    public static async Task<SavedSession?> LoadAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(FilePath))
                return null;

            var protectedBytes = await File.ReadAllBytesAsync(FilePath, ct);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<SavedSession>(json, JsonOptions);
        }
        catch
        {
            Clear();
            return null;
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
        catch
        {
            // Suppression best-effort.
        }
    }
}
