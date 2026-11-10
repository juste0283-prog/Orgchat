namespace OrgChat.Client.Services;

/// <summary>Erreur renvoyée par l'API OrgChat ou par le réseau.</summary>
public sealed class ApiException : Exception
{
    public int StatusCode { get; }

    public ApiException(string message, int statusCode = 0, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}
