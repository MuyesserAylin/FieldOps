using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FieldOps.Api.Domain;
using FieldOps.Api.Tests.Auth;
using FieldOps.Api.Tests.Database;
using FieldOps.Api.Tests.Reports;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Tests.Users;

[Collection("Database")]
[Trait("Category", "Integration")]
public class UsersApiTests : IAsyncLifetime
{
    private const string NewPassword = "another-fake-pass-1";

    private readonly PostgisFixture _fixture;
    private readonly FieldOpsApiFactory _factory;

    public UsersApiTests(PostgisFixture fixture)
    {
        _fixture = fixture;
        _factory = new FieldOpsApiFactory(fixture.ConnectionString);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    public static TheoryData<string, string> InvalidRequests => new()
    {
        { "username", """{"username":"ab","password":"valid-fake-pass-1","role":"Operator"}""" },
        { "username", """{"username":"bad name!","password":"valid-fake-pass-1","role":"Operator"}""" },
        { "username", """{"password":"valid-fake-pass-1","role":"Operator"}""" },
        { "password", """{"username":"good-name","password":"short","role":"Operator"}""" },
        { "password", """{"username":"good-name","role":"Operator"}""" },
        { "role", """{"username":"good-name","password":"valid-fake-pass-1","role":"Boss"}""" },
        { "role", """{"username":"good-name","password":"valid-fake-pass-1"}""" },
    };

    private static object NewUserBody(string username = "new-user", string role = "Operator")
        => new { username, password = NewPassword, role };

    [Fact]
    public async Task CreateUser_WithoutToken_Returns401Problem()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users", NewUserBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(UserRole.Team)]
    [InlineData(UserRole.Operator)]
    public async Task CreateUser_AsNonAdmin_Returns403ProblemAndSavesNothing(UserRole role)
    {
        var client = await AuthTestHelper.CreateClientAsync(_factory, _fixture, "fake-actor", role);

        var response = await client.PostAsJsonAsync("/api/users", NewUserBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        await using var db = _fixture.CreateDbContext();
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task CreateUser_AsAdmin_Returns201WithoutSecretsAndStoresHash()
    {
        var client = await AuthTestHelper.CreateClientAsync(
            _factory, _fixture, "fake-admin", UserRole.Admin);

        var response = await client.PostAsJsonAsync("/api/users", NewUserBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var text = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;

        Assert.Equal("new-user", root.GetProperty("username").GetString());
        Assert.Equal("Operator", root.GetProperty("role").GetString());
        Assert.True(root.GetProperty("isActive").GetBoolean());
        Assert.True(root.TryGetProperty("id", out _));
        Assert.True(root.TryGetProperty("createdAt", out _));
        Assert.False(root.TryGetProperty("password", out _));
        Assert.False(root.TryGetProperty("passwordHash", out _));
        Assert.DoesNotContain(NewPassword, text);

        await using var db = _fixture.CreateDbContext();
        var stored = await db.Users.SingleAsync(u => u.UserName == "new-user");
        Assert.NotEqual(NewPassword, stored.PasswordHash);
        Assert.DoesNotContain(NewPassword, stored.PasswordHash);
    }

    [Fact]
    public async Task CreatedUser_CanLoginAndSeesOwnRole()
    {
        var adminClient = await AuthTestHelper.CreateClientAsync(
            _factory, _fixture, "fake-admin", UserRole.Admin);

        var create = await adminClient.PostAsJsonAsync(
            "/api/users", NewUserBody("new-team", "Team"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new { username = "new-team", password = NewPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = loginJson.RootElement.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        using var meJson = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.Equal("new-team", meJson.RootElement.GetProperty("username").GetString());
        Assert.Equal("Team", meJson.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public async Task CreateUser_WithSameUserNameInDifferentCase_Returns409Problem()
    {
        var client = await AuthTestHelper.CreateClientAsync(
            _factory, _fixture, "fake-admin", UserRole.Admin);

        var first = await client.PostAsJsonAsync("/api/users", NewUserBody("Ali.Fake"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/users", NewUserBody("ali.fake"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);

        await using var db = _fixture.CreateDbContext();
        Assert.Equal(2, await db.Users.CountAsync());
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task CreateUser_WithInvalidRequest_Returns400AndSavesNothing(
        string field, string body)
    {
        var client = await AuthTestHelper.CreateClientAsync(
            _factory, _fixture, "fake-admin", UserRole.Admin);
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/users", content);

        await ProblemAssert.HasFieldErrorAsync(response, field);

        await using var db = _fixture.CreateDbContext();
        Assert.Equal(1, await db.Users.CountAsync());
    }
}