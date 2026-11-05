namespace OrgChat.Core.Contracts.Services;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
}
