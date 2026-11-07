using FieldOps.Api.Auth;
using FieldOps.Api.Domain;
using FieldOps.Api.Tests.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FieldOps.Api.Tests.Auth;

[Collection("Database")]
[Trait("Category", "Integration")]
public class UserSeederTests : IAsyncLifetime
{
    private const string FakePassword = "fake-admin-password-1";

    private readonly PostgisFixture _fixture;
    private readonly FakeClock _clock =
        new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    public UserSeederTests(PostgisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static IConfiguration BuildConfiguration(string? username, string? password)
    {
        var values = new Dictionary<string, string?>
        {
            ["Seed:AdminUsername"] = username,
            ["Seed:AdminPassword"] = password
        };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private async Task<bool> RunSeederAsync(IConfiguration configuration)
    {
        await using var db = _fixture.CreateDbContext();
        var seeder = new UserSeeder(db, new PasswordService(), configuration, _clock);
        return await seeder.SeedAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Seed_EmptyTableWithSettings_CreatesAdmin()
    {
        var created = await RunSeederAsync(BuildConfiguration("Fake-Admin", FakePassword));

        Assert.True(created);

        await using var db = _fixture.CreateDbContext();
        var user = await db.Users.SingleAsync();
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.True(user.IsActive);
        Assert.NotEqual(FakePassword, user.PasswordHash);
        Assert.True(new PasswordService().Verify(user.PasswordHash, FakePassword));
    }

    [Fact]
    public async Task Seed_TableAlreadyHasUsers_DoesNothing()
    {
        await using (var db = _fixture.CreateDbContext())
        {
            db.Users.Add(new User(
                "fake-existing", new PasswordService().Hash("another-fake-password"),
                UserRole.Team, _clock.UtcNow));
            await db.SaveChangesAsync();
        }

        var created = await RunSeederAsync(BuildConfiguration("Fake-Admin", FakePassword));

        Assert.False(created);

        await using var checkDb = _fixture.CreateDbContext();
        var user = await checkDb.Users.SingleAsync();
        Assert.Equal(UserRole.Team, user.Role);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("fake-admin", null)]
    [InlineData(null, FakePassword)]
    [InlineData("", "")]
    public async Task Seed_WhenSettingsAreMissing_DoesNothing(string? username, string? password)
    {
        var created = await RunSeederAsync(BuildConfiguration(username, password));

        Assert.False(created);

        await using var db = _fixture.CreateDbContext();
        Assert.Equal(0, await db.Users.CountAsync());
    }
}