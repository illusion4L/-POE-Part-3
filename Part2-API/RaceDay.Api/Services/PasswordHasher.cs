using System.Security.Cryptography;

namespace RaceDay.Api.Services;

public interface IPasswordHasher
{
    (byte[] Hash, byte[] Salt) Hash(string password);
    bool Verify(string password, byte[] hash, byte[] salt);
}

/// <summary>PBKDF2 (SHA-256, 100k iterations) with a random 16-byte salt per user.</summary>
public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public (byte[] Hash, byte[] Salt) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return (hash, salt);
    }

    public bool Verify(string password, byte[] hash, byte[] salt)
    {
        var attempt = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return CryptographicOperations.FixedTimeEquals(attempt, hash);
    }
}
