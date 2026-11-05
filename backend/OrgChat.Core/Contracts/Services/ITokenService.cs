namespace OrgChat.Core.Contracts.Services;

public interface ITokenService
{
    string GenerateToken(int userId, string username, string role);
}
