using FieldOps.Api.Domain;
using FieldOps.Api.Teams;
using FieldOps.Api.Tests.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests.Teams;

[Collection("Database")]
[Trait("Category", "Integration")]
public class DemoTeamSeederTests : IAsyncLifetime
{
    private const int ShiftDays = 45;
    private const int DayShiftStartHourUtc = 5;
    private const int NightShiftStartHourUtc = 17;

    private static readonly DateTimeOffset NoonUtc = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgisFixture _fixture;

    public DemoTeamSeederTests(PostgisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static IConfiguration BuildConfiguration(string? demoTeams)
    {
        var values = new Dictionary<string, string?> { ["Seed:DemoTeams"] = demoTeams };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private async Task<bool> RunSeederAsync(string? demoTeams, DateTimeOffset? now = null)
    {
        await using var db = _fixture.CreateDbContext();
        var clock = new FakeClock(now ?? NoonUtc);
        var seeder = new DemoTeamSeeder(db, BuildConfiguration(demoTeams), clock);
        return await seeder.SeedAsync(CancellationToken.None);
    }

    private async Task<List<Team>> LoadTeamsAsync()
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Teams
            .Include(t => t.Skills)
            .Include(t => t.Shifts)
            .AsSplitQuery()
            .OrderBy(t => t.Id)
            .ToListAsync();
    }

    private static bool IsDayTeam(Team team) =>
        team.Shifts.All(s => s.StartsAt.UtcDateTime.Hour == DayShiftStartHourUtc);

    private static bool IsNightTeam(Team team) =>
        team.Shifts.All(s => s.StartsAt.UtcDateTime.Hour == NightShiftStartHourUtc);


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    public async Task Seed_WhenSettingIsNotTrue_DoesNothing(string? setting)
    {
        var seeded = await RunSeederAsync(setting);

        Assert.False(seeded);

        await using var db = _fixture.CreateDbContext();
        Assert.Equal(0, await db.Teams.CountAsync());
    }

    [Fact]
    public async Task Seed_SecondRun_DoesNothing()
    {
        var first = await RunSeederAsync("true");
        var second = await RunSeederAsync("true");

        Assert.True(first);
        Assert.False(second);

        await using var db = _fixture.CreateDbContext();
        Assert.Equal(24, await db.Teams.CountAsync());
        Assert.Equal(24 * ShiftDays, await db.Shifts.CountAsync());
    }

    [Fact]
    public async Task Seed_TableAlreadyHasTeam_DoesNothing()
    {
        await using (var db = _fixture.CreateDbContext())
        {
            db.Teams.Add(new Team("Fake Existing Team", new Point(30.5, 39.8) { SRID = 4326 }, NoonUtc));
            await db.SaveChangesAsync();
        }

        var seeded = await RunSeederAsync("true");

        Assert.False(seeded);

        await using var checkDb = _fixture.CreateDbContext();
        var team = await checkDb.Teams.SingleAsync();
        Assert.Equal("Fake Existing Team", team.Name);
    }


    [Fact]
    public async Task Seed_EmptyTableWithSettingOn_CreatesTwentyFourUniqueTeams()
    {
        var seeded = await RunSeederAsync("true");

        Assert.True(seeded);

        var teams = await LoadTeamsAsync();
        Assert.Equal(24, teams.Count);
        Assert.Equal(24, teams.Select(t => t.Name).Distinct().Count());
    }

    [Fact]
    public async Task Seed_Teams_AreInsideServiceAreaWithSridAndTimestamp()
    {
        await RunSeederAsync("true");

        var teams = await LoadTeamsAsync();

        Assert.All(teams, t =>
        {
            Assert.Equal(4326, t.Location.SRID);
            Assert.InRange(t.Location.Y, ServiceArea.MinLatitude, ServiceArea.MaxLatitude);
            Assert.InRange(t.Location.X, ServiceArea.MinLongitude, ServiceArea.MaxLongitude);
            Assert.Equal(NoonUtc, t.LocationUpdatedAt);
        });
    }

    [Fact]
    public async Task Seed_Skills_HaveExpectedCounts()
    {
        await RunSeederAsync("true");

        await using var db = _fixture.CreateDbContext();
        var names = await db.TeamSkills.Select(ts => ts.Skill.Name).ToListAsync();
        var counts = names.GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(28, names.Count);
        Assert.Equal(7, counts["Gas"]);
        Assert.Equal(8, counts["Plumbing"]);
        Assert.Equal(6, counts["Electrical"]);
        Assert.Equal(7, counts["RoadWork"]);

        var teams = await LoadTeamsAsync();
        Assert.All(teams, t => Assert.NotEmpty(t.Skills));
    }

    [Fact]
    public async Task Seed_Teams_HaveOneOfflineTeamAndFullShiftSchedule()
    {
        await RunSeederAsync("true");

        var teams = await LoadTeamsAsync();

        var offline = Assert.Single(teams, t => t.Status == TeamStatus.Offline);
        Assert.Equal("RoadWork Team B", offline.Name);
        Assert.Equal(23, teams.Count(t => t.Status == TeamStatus.Idle));

        Assert.All(teams, t =>
        {
            Assert.Equal(ShiftDays, t.Shifts.Count);
            Assert.All(t.Shifts, s => Assert.Equal(TimeSpan.FromHours(12), s.EndsAt - s.StartsAt));
        });
    }

    [Fact]
    public async Task Seed_Teams_SplitEvenlyBetweenDayAndNight()
    {
        await RunSeederAsync("true");

        var teams = await LoadTeamsAsync();

        Assert.Equal(12, teams.Count(IsDayTeam));
        Assert.Equal(12, teams.Count(IsNightTeam));
    }


    [Theory]
    [InlineData(0, 0, 12)]
    [InlineData(5, 12, 0)]
    [InlineData(9, 12, 0)]
    [InlineData(17, 0, 12)]
    public async Task Seed_ShiftsCoverTheSeedMoment(int utcHour, int expectedDayTeams, int expectedNightTeams)
    {
        var now = new DateTimeOffset(2026, 1, 1, utcHour, 0, 0, TimeSpan.Zero);
        await RunSeederAsync("true", now);

        var teams = await LoadTeamsAsync();
        var onShift = teams
            .Where(t => t.Shifts.Any(s => s.StartsAt <= now && now < s.EndsAt))
            .ToList();

        Assert.Equal(expectedDayTeams, onShift.Count(IsDayTeam));
        Assert.Equal(expectedNightTeams, onShift.Count(IsNightTeam));
    }
}