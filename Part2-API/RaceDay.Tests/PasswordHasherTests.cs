using RaceDay.Api.Services;

namespace RaceDay.Tests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_DoesNotStorePlainText()
    {
        var (hash, _) = _hasher.Hash("Passw0rd!");
        Assert.DoesNotContain("Passw0rd!", System.Text.Encoding.UTF8.GetString(hash));
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var (hash, salt) = _hasher.Hash("Passw0rd!");
        Assert.True(_hasher.Verify("Passw0rd!", hash, salt));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var (hash, salt) = _hasher.Hash("Passw0rd!");
        Assert.False(_hasher.Verify("wrong", hash, salt));
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentSaltsAndHashes()
    {
        var a = _hasher.Hash("Passw0rd!");
        var b = _hasher.Hash("Passw0rd!");
        Assert.NotEqual(a.Salt, b.Salt);
        Assert.NotEqual(a.Hash, b.Hash);
    }
}
