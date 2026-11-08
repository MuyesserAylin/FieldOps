using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Api.Auth;
using FieldOps.Api.Domain;
using FieldOps.Api.Tests.Database;
using FieldOps.Api.Tests.Reports;

namespace FieldOps.Api.Tests.Auth;

public static class AuthTestHelper
{
    public const string FakePassword = "fake-test-password-1";

    public static async Task AddUserAsync(
        PostgisFixture fixture, string username, UserRole role, bool isActive = true)
    {
        await using var db = fixture.CreateDbContext();
        var hash = new PasswordService().Hash(FakePassword);
        db.Users.Add(new User(username, hash, role, DateTimeOffset.UtcNow, isActive));
        await db.SaveChangesAsync();
    }

    public static async Task<string> LoginAsync(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { username, password = FakePassword });
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("accessToken").GetString()!;
    }

    public static async Task<HttpClient> CreateClientAsync(
        FieldOpsApiFactory factory, PostgisFixture fixture, string username, UserRole role)
    {
        await AddUserAsync(fixture, username, role);

        var client = factory.CreateClient();
        var token = await LoginAsync(client, username);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}