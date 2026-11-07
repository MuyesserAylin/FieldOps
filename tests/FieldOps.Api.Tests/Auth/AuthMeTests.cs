using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Api.Auth;
using FieldOps.Api.Domain;
using FieldOps.Api.Tests.Database;
using FieldOps.Api.Tests.Reports;
using Microsoft.Extensions.Configuration;

namespace FieldOps.Api.Tests.Auth;

[Collection("Database")]
[Trait("Category", "Integration")]
public class AuthMeTests : IAsyncLifetime
{
    private readonly PostgisFixture _fixture;
    private readonly FieldOpsApiFactory _factory;

    public AuthMeTests(PostgisFixture fixture)
    {
        _fixture = fixture;
        _factory = new FieldOpsApiFactory(fixture.ConnectionString);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private static string CreateToken(string key, DateTimeOffset issuedAt)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "FieldOps",
            ["Jwt:Audience"] = "FieldOps",
            ["Jwt:ExpiryMinutes"] = "60",
            ["Jwt:Key"] = key
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var service = new TokenService(
            JwtSettings.FromConfiguration(configuration), new FakeClock(issuedAt));

        var user = new User("fake-operator", "fake-hash-value", UserRole.Operator, issuedAt);
        return service.CreateToken(user).Value;
    }

    private HttpClient CreateClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401Problem()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Me_WithValidToken_Returns200WithUsernameAndRole()
    {
        var token = CreateToken(FieldOpsApiFactory.TestJwtKey, DateTimeOffset.UtcNow);
        var client = CreateClientWithToken(token);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("fake-operator", json.RootElement.GetProperty("username").GetString());
        Assert.Equal("Operator", json.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Me_WithTokenSignedByAnotherKey_Returns401()
    {
        var token = CreateToken(new string('x', 40), DateTimeOffset.UtcNow);
        var client = CreateClientWithToken(token);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithExpiredToken_Returns401()
    {
        var longAgo = DateTimeOffset.UtcNow.AddDays(-2);
        var token = CreateToken(FieldOpsApiFactory.TestJwtKey, longAgo);
        var client = CreateClientWithToken(token);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}