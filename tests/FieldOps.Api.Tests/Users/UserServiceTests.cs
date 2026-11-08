using FieldOps.Api.Auth;
using FieldOps.Api.Data;
using FieldOps.Api.Domain;
using FieldOps.Api.Tests.Database;
using FieldOps.Api.Users;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Tests.Users;

[Collection("Database")]
[Trait("Category", "Integration")]
public class UserServiceTests : IAsyncLifetime
{
    private const string FakePassword = "fake-test-password-1";

    private readonly PostgisFixture _fixture;
    private readonly FakeClock _clock =
        new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    public UserServiceTests(PostgisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private UserService CreateService(AppDbContext db)
        => new(db, new PasswordService(), _clock);

    private static ValidatedUser Validated(string userName = "fake-user")
        => new(userName, FakePassword, UserRole.Operator);

    [Fact]
    public async Task Create_NewUser_SavesHashedPasswordAndReturnsUserWithoutSecrets()
    {
        await using var db = _fixture.CreateDbContext();

        var result = await CreateService(db).CreateAsync(Validated(), CancellationToken.None);

        Assert.Equal(CreateUserOutcome.Created, result.Outcome);
        Assert.NotNull(result.User);
        Assert.Equal("fake-user", result.User.Username);
        Assert.Equal("Operator", result.User.Role);
        Assert.True(result.User.IsActive);

        await using var checkDb = _fixture.CreateDbContext();
        var stored = await checkDb.Users.SingleAsync();
        Assert.NotEqual(FakePassword, stored.PasswordHash);
        Assert.True(new PasswordService().Verify(stored.PasswordHash, FakePassword));
    }

    [Fact]
    public async Task Create_ExistingUserName_ReturnsDuplicateAndKeepsOneRow()
    {
        await using (var firstDb = _fixture.CreateDbContext())
        {
            await CreateService(firstDb).CreateAsync(Validated(), CancellationToken.None);
        }

        await using var secondDb = _fixture.CreateDbContext();
        var result = await CreateService(secondDb).CreateAsync(Validated(), CancellationToken.None);

        Assert.Equal(CreateUserOutcome.DuplicateUserName, result.Outcome);
        Assert.Null(result.User);

        await using var checkDb = _fixture.CreateDbContext();
        Assert.Equal(1, await checkDb.Users.CountAsync());
    }

    [Fact]
    public async Task Create_SameUserNameInParallel_ExactlyOneWins()
    {
        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var db = _fixture.CreateDbContext();
            return await CreateService(db).CreateAsync(Validated(), CancellationToken.None);
        });

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r.Outcome == CreateUserOutcome.Created));
        Assert.Equal(4, results.Count(r => r.Outcome == CreateUserOutcome.DuplicateUserName));

        await using var checkDb = _fixture.CreateDbContext();
        Assert.Equal(1, await checkDb.Users.CountAsync());
    }
}