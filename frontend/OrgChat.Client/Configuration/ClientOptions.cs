using System.Text.Json;

namespace OrgChat.Client.Configuration;

/// <summary>
/// Paramètres de connexion du client. Peuvent être surchargés par la variable
/// d'environnement ORGCHAT_BASEURL ou directement depuis l'écran de connexion.
/// </summary>
public sealed class ClientOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5000";

    public int PageSize { get; set; } = 50;

    public static ClientOptions Load()
    {
        var options = new ClientOptions();
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        try
        {
            if (File.Exists(path))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                if (document.RootElement.TryGetProperty("OrgChat", out var section))
                {
                    if (section.TryGetProperty("BaseUrl", out var baseUrl) && baseUrl.ValueKind == JsonValueKind.String)
                        options.BaseUrl = baseUrl.GetString() ?? options.BaseUrl;

                    if (section.TryGetProperty("PageSize", out var pageSize) && pageSize.TryGetInt32(out var size) && size > 0)
                        options.PageSize = Math.Min(size, 100);
                }
            }
        }
        catch
        {
            // Configuration invalide : on garde les valeurs par défaut.
        }

        var envUrl = Environment.GetEnvironmentVariable("ORGCHAT_BASEURL");
        if (!string.IsNullOrWhiteSpace(envUrl))
            options.BaseUrl = envUrl;

        options.BaseUrl = ApiUrl.Normalize(options.BaseUrl);
        return options;
    }
}

internal static class ApiUrl
{
    public static string Normalize(string url)
    {
        url = (url ?? string.Empty).Trim();
        if (url.Length == 0)
            return "http://localhost:5000";

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "http://" + url;
        }

        return url.TrimEnd('/');
    }
}
