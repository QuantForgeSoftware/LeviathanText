using System.Security.Cryptography;
using System.Text;

namespace LeviathanText.Infrastructure;

/// <summary>API-key generation and hashing for local accounts. Plaintext keys are shown once.</summary>
public static class UserKeys
{
    public static string NewKey() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public static bool Matches(string provided, string expectedHash) =>
        CryptographicEquals(Hash(provided), expectedHash);

    private static bool CryptographicEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
