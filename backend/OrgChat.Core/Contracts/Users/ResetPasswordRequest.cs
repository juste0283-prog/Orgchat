namespace OrgChat.Core.Contracts.Users;

public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}
