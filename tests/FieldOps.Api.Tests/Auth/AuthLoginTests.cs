using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Api.Auth;
using FieldOps.Api.Domain;
using FieldOps.Api.Tests.Database;
using FieldOps.Api.Tests.Reports;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Tests.Auth;

[Collection("Database")]
[Trait("Category", "Integration")]
public class AuthLoginTests : IAsyncLifetime
{
    private const string FakePassword = "fake-test-password-1";

    private readonly PostgisFixture _fixture;
    private readonly FieldOpsApiFactory _factory;

    public AuthLoginTests(PostgisFixture fixture)
    {
        _fixture = fixture;
        _factory = new FieldOpsApiFactory(fixture.ConnectionString);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task AddUserAsync(string username, UserRole role, bool isActive = true)
    {
        await using var db = _fixture.CreateDbContext();
        var hash = new PasswordService().Hash(FakePassword);
        db.Users.Add(new User(username, hash, role, DateTimeOffset.UtcNow, isActive));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithTokenAndRole()
    {
        await AddUserAsync("fake-operator", UserRole.Operator);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "Fake-Operator", password = FakePassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var text = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(text);
        Assert.False(string.IsNullOrWhiteSpace(
            json.RootElement.GetProperty("accessToken").GetString()));
        Assert.Equal("Operator", json.RootElement.GetProperty("role").GetString());
        Assert.DoesNotContain("hash", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(FakePassword, text);
    }

    [Theory]
    [InlineData("fake-operator", "wrong-fake-password")]
    [InlineData("nobody", FakePassword)]
    [InlineData("fake-inactive", FakePassword)]
    public async Task Login_WithBadCredentials_Returns401WithSameProblemBody(
        string username, string password)
    {
        await AddUserAsync("fake-operator", UserRole.Operator);
        await AddUserAsync("fake-inactive", UserRole.Team, isActive: false);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { username, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Invalid username or password.", json.RootElement.GetProperty("title").GetString());
        Assert.Equal(401, json.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Login_WithMissingFields_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { username = "fake-operator" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StoredPasswordHash_IsNotThePlainPassword()
    {
        await AddUserAsync("fake-operator", UserRole.Operator);

        await using var db = _fixture.CreateDbContext();
        var user = await db.Users.SingleAsync();

        Assert.NotEqual(FakePassword, user.PasswordHash);
        Assert.DoesNotContain(FakePassword, user.PasswordHash);
    }
}