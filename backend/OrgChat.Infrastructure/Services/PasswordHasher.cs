using System.Security.Cryptography;
using OrgChat.Core.Contracts.Services;

namespace OrgChat.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be empty.", nameof(password));

        using var rng = RandomNumberGenerator.Create();
        var salt = new byte[SaltSize];
        rng.GetBytes(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(HashSize);

        var hashBytes = new byte[SaltSize + HashSize];
        Buffer.BlockCopy(salt, 0, hashBytes, 0, SaltSize);
        Buffer.BlockCopy(hash, 0, hashBytes, SaltSize, HashSize);

        return Convert.ToBase64String(hashBytes);
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password))
            return false;
        if (string.IsNullOrEmpty(storedHash))
            return false;

        var hashBytes;
        try
        {
            hashBytes = Convert.FromBase64String(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        if (hashBytes.Length != SaltSize + HashSize)
            return false;

        var salt = new byte[SaltSize];
        Buffer.BlockCopy(hashBytes, 0, salt, 0, SaltSize);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(HashSize);

        // Comparaison à temps constant pour éviter les attaques par timing
        var areEqual = true;
        for (var i = 0; i < HashSize; i++)
        {
            if (hashBytes[SaltSize + i] != hash[i])
                areEqual = false;
        }
        return areEqual;
    }
}
