namespace OrgChat.Core.Contracts.Users;

public class SuspendUserRequest
{
    /// <summary>Durée de suspension en jours. Si null et SuspendUntil absent : 7 jours.</summary>
    public int? DurationDays { get; set; }
    public DateTime? SuspendUntil { get; set; }
    public string? Reason { get; set; }
}

public class SessionDto
{
    public int Id { get; set; }
    public string TokenId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class ActivityItemDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserImportRowResult
{
    public int RowNumber { get; set; }
    public string? Email { get; set; }
    public bool Created { get; set; }
    public string? Error { get; set; }
}

public class UserImportResultDto
{
    public int Total { get; set; }
    public int Created { get; set; }
    public int Skipped { get; set; }
    public List<UserImportRowResult> Rows { get; set; } = new();
    /// <summary>Jetons d'activation générés pour les comptes créés (copiables).</summary>
    public List<ImportedActivationDto> Activations { get; set; } = new();
}

public class ImportedActivationDto
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ActivationToken { get; set; } = string.Empty;
    public string ActivationLink { get; set; } = string.Empty;
}

public class ResetPasswordResultDto
{
    public string ResetToken { get; set; } = string.Empty;
    public string ResetLink { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
