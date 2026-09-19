using DevDocSpace.Api.Auth;

namespace DevDocSpace.Api.Tests;

public class ApiKeyHasherTests
{
    [Fact]
    public void Generated_key_verifies_against_its_hash()
    {
        var (plain, prefix, hash) = ApiKeyHasher.Generate();
        Assert.StartsWith("dds_", plain);
        Assert.Equal(plain[..12], prefix);
        Assert.True(ApiKeyHasher.Verify(plain, hash));
        Assert.False(ApiKeyHasher.Verify(plain + "x", hash));
    }

    [Fact]
    public void Keys_are_unique()
    {
        var a = ApiKeyHasher.Generate();
        var b = ApiKeyHasher.Generate();
        Assert.NotEqual(a.PlainText, b.PlainText);
    }
}
