using System.Security.Cryptography;
using System.Text;

namespace DevDocSpace.Api.Auth;

public static class ApiKeyHasher
{
    public const string KeyPrefix = "dds_";

    public static (string PlainText, string Prefix, string Hash) Generate()
    {
        var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        var plain = KeyPrefix + secret;
        return (plain, ExtractPrefix(plain), Hash(plain));
    }

    public static string ExtractPrefix(string plain) => plain[..Math.Min(plain.Length, KeyPrefix.Length + 8)];

    public static string Hash(string plain) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(plain)));

    public static bool Verify(string plain, string hash) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Hash(plain)), Encoding.UTF8.GetBytes(hash));
}
