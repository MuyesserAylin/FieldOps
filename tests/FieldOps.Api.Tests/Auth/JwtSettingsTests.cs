using FieldOps.Api.Auth;
using Microsoft.Extensions.Configuration;

namespace FieldOps.Api.Tests.Auth;

public class JwtSettingsTests
{
    private static IConfiguration BuildConfiguration(
        string? key, string expiry = "60")
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "FieldOps",
            ["Jwt:Audience"] = "FieldOps",
            ["Jwt:ExpiryMinutes"] = expiry,
            ["Jwt:Key"] = key
        };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void FromConfiguration_ValidSettings_ReadsAllValues()
    {
        var settings = JwtSettings.FromConfiguration(
            BuildConfiguration(new string('k', 40)));

        Assert.Equal("FieldOps", settings.Issuer);
        Assert.Equal("FieldOps", settings.Audience);
        Assert.Equal(60, settings.ExpiryMinutes);
        Assert.Equal(40, settings.KeyBytes.Length);
    }

    [Fact]
    public void FromConfiguration_MissingKey_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            JwtSettings.FromConfiguration(BuildConfiguration(null)));
    }

    [Fact]
    public void FromConfiguration_KeyOneByteTooShort_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            JwtSettings.FromConfiguration(BuildConfiguration(new string('k', 31))));
    }

    [Fact]
    public void FromConfiguration_KeyExactlyMinimumLength_IsAccepted()
    {
        var settings = JwtSettings.FromConfiguration(
            BuildConfiguration(new string('k', 32)));

        Assert.Equal(32, settings.KeyBytes.Length);
    }

    [Fact]
    public void FromConfiguration_ShortKey_ErrorMessageDoesNotContainTheKey()
    {
        var shortKey = "short-fake-test-key";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            JwtSettings.FromConfiguration(BuildConfiguration(shortKey)));

        Assert.DoesNotContain(shortKey, exception.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    public void FromConfiguration_InvalidExpiry_Throws(string expiry)
    {
        Assert.Throws<InvalidOperationException>(() =>
            JwtSettings.FromConfiguration(BuildConfiguration(new string('k', 40), expiry)));
    }
}