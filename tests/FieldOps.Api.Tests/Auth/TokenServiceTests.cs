using FieldOps.Api.Auth;
using FieldOps.Api.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FieldOps.Api.Tests.Auth;

public class TokenServiceTests
{
    private static readonly string FakeKey = new('k', 40);
    private static readonly DateTimeOffset Now =
        new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static TokenService CreateService(string key)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "FieldOps",
            ["Jwt:Audience"] = "FieldOps",
            ["Jwt:ExpiryMinutes"] = "60",
            ["Jwt:Key"] = key
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new TokenService(JwtSettings.FromConfiguration(configuration), new FakeClock(Now));
    }

    private static User CreateUser(UserRole role) =>
        new("fake-user", "fake-hash-value", role, Now);

    [Fact]
    public void CreateToken_ContainsExpectedClaimsAndExpiry()
    {
        var service = CreateService(FakeKey);

        var result = service.CreateToken(CreateUser(UserRole.Operator));
        var token = new JsonWebTokenHandler().ReadJsonWebToken(result.Value);

        Assert.Equal("fake-user", token.GetClaim("name").Value);
        Assert.Equal("Operator", token.GetClaim("role").Value);
        Assert.Equal("FieldOps", token.Issuer);
        Assert.Contains("FieldOps", token.Audiences);
        Assert.Equal(Now.AddMinutes(60), result.ExpiresAt);
        Assert.Equal(Now.AddMinutes(60).UtcDateTime, token.ValidTo);
    }

    [Fact]
    public void CreateToken_DoesNotContainThePasswordHash()
    {
        var service = CreateService(FakeKey);

        var result = service.CreateToken(CreateUser(UserRole.Team));
        var payload = new JsonWebTokenHandler().ReadJsonWebToken(result.Value).EncodedPayload;
        var decoded = System.Text.Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(payload));

        Assert.DoesNotContain("fake-hash-value", decoded);
    }

    [Fact]
    public async Task CreateToken_SignatureIsValidOnlyWithTheSameKey()
    {
        var service = CreateService(FakeKey);
        var result = service.CreateToken(CreateUser(UserRole.Admin));
        var handler = new JsonWebTokenHandler();

        TokenValidationParameters Parameters(string key) => new()
        {
            ValidIssuer = "FieldOps",
            ValidAudience = "FieldOps",
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = false
        };

        var withSameKey = await handler.ValidateTokenAsync(result.Value, Parameters(FakeKey));
        var withOtherKey = await handler.ValidateTokenAsync(
            result.Value, Parameters(new string('x', 40)));

        Assert.True(withSameKey.IsValid);
        Assert.False(withOtherKey.IsValid);
    }
}