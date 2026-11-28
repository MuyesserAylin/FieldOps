using FieldOps.Api.Assignment;
using FieldOps.Api.Domain;
using FieldOps.Api.Teams;
using FieldOps.Api.Tests.Assignment;
using FieldOps.Api.Tests.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests.Teams;

[Collection("Database")]
[Trait("Category", "Integration")]
public class DemoLocationRefresherTests : IAsyncLifetime
{
    private static readonly DateTimeOffset SeedTime = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private readonly PostgisFixture _fixture;

    public DemoLocationRefresherTests(PostgisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;


    private static IConfiguration Config(bool? demoTeams) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(demoTeams is { } value
                ? new Dictionary<string, string?> { ["Seed:DemoTeams"] = value ? "true" : "false" }
                : new Dictionary<string, string?>())
            .Build();

    private async Task SeedAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var seeded = await new DemoTeamSeeder(db, Config(true), new FakeClock(SeedTime))
            .SeedAsync(CancellationToken.None);
        Assert.True(seeded);
    }

    private async Task<int> RefreshAsync(DateTimeOffset at, IConfiguration config)
    {
        await using var db = _fixture.CreateDbContext();
        var refresher = new DemoLocationRefresher(db, new FakeClock(at), config);
        return await refresher.RefreshAllAsync(CancellationToken.None);
    }

    private sealed record TeamRow(
        int Id, string Name, TeamStatus Status, double X, double Y,
        DateTimeOffset? CooldownUntil, DateTimeOffset LocationUpdatedAt);

    private async Task<List<TeamRow>> ReadTeamsAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var teams = await db.Teams.AsNoTracking().OrderBy(t => t.Id).ToListAsync();

        return teams
            .Select(t => new TeamRow(
                t.Id, t.Name, t.Status, t.Location.X, t.Location.Y, t.CooldownUntil, t.LocationUpdatedAt))
            .ToList();
    }


    [Fact]
    public async Task RefreshAll_SetsEveryTeamsLocationTimeToNow_AndChangesNothingElse()
    {
        await SeedAsync();
        var before = await ReadTeamsAsync();
        int shiftsBefore;
        await using (var db = _fixture.CreateDbContext())
        {
            shiftsBefore = await db.Shifts.CountAsync();
        }

        var later = SeedTime.AddMinutes(10);
        var refreshed = await RefreshAsync(later, Config(true));

        var after = await ReadTeamsAsync();
        Assert.Equal(24, refreshed);
        Assert.Equal(24, after.Count);
        Assert.All(after, t => Assert.Equal(later, t.LocationUpdatedAt));

        Assert.Equal(
            before.Select(t => t with { LocationUpdatedAt = default }),
            after.Select(t => t with { LocationUpdatedAt = default }));
        Assert.Equal(TeamStatus.Offline, after.Single(t => t.Name == "RoadWork Team B").Status);

        await using var verifyDb = _fixture.CreateDbContext();
        Assert.Equal(shiftsBefore, await verifyDb.Shifts.CountAsync());
    }

    [Fact]
    public async Task RefreshAll_WithNoTeams_ReturnsZero()
    {
        Assert.Equal(0, await RefreshAsync(SeedTime, Config(true)));
    }

    [Fact]
    public async Task RefreshAll_SkipsATeamWhoseLocationTimeIsInTheFuture()
    {
        var future = SeedTime.AddHours(1);
        await using (var db = _fixture.CreateDbContext())
        {
            var location = new Point(30.5206, 39.7767) { SRID = 4326 };
            db.Teams.Add(new Team("Future", location, future));
            await db.SaveChangesAsync();
        }

        var refreshed = await RefreshAsync(SeedTime, Config(true));

        Assert.Equal(0, refreshed);
        Assert.Equal(future, Assert.Single(await ReadTeamsAsync()).LocationUpdatedAt);
    }


    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task WhenDemoModeIsNotOn_IsDisabled_AndRefreshingThrows_AndChangesNothing(bool? demoTeams)
    {
        await SeedAsync();
        var before = await ReadTeamsAsync();
        var config = Config(demoTeams);

        await using (var db = _fixture.CreateDbContext())
        {
            Assert.False(new DemoLocationRefresher(db, new FakeClock(SeedTime), config).IsEnabled);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => RefreshAsync(SeedTime.AddMinutes(10), config));

        Assert.Equal(before, await ReadTeamsAsync());
    }

    [Fact]
    public async Task WhenDemoModeIsOn_IsEnabled()
    {
        await using var db = _fixture.CreateDbContext();

        Assert.True(new DemoLocationRefresher(db, new FakeClock(SeedTime), Config(true)).IsEnabled);
    }


    private async Task<RecommendationResult> RecommendAsync(long jobId, DateTimeOffset now)
    {
        await using var db = _fixture.CreateDbContext();
        var settings = Options.Create(TestSettings.Standard());

        var service = new TeamRecommendationService(
            db,
            new JobRequestLoader(db, settings),
            new TeamSnapshotLoader(db),
            new ScoringTeamLoader(db),
            new FakeClock(now),
            settings);

        return await service.RecommendAsync(jobId, CancellationToken.None);
    }

    [Fact]
    public async Task Story_StaleLocationsEliminateTheElectricalTeams_ThenRefreshingBringsTheCandidatesBack()
    {
        await SeedAsync();

        long jobId;
        await using (var db = _fixture.CreateDbContext())
        {
            var faultType = await db.FaultTypes.SingleAsync(f => f.Name == "Street light failure");
            var center = new Point(ServiceArea.CenterLongitude, ServiceArea.CenterLatitude) { SRID = 4326 };
            var job = new Job(faultType, center, "Story test", Guid.NewGuid().ToString("N")[..16], SeedTime);
            db.Jobs.Add(job);
            await db.SaveChangesAsync();
            jobId = job.Id;
        }

        var later = SeedTime.AddMinutes(10);

        var stale = await RecommendAsync(jobId, later);
        Assert.Equal(RecommendationOutcome.NoCandidates, stale.Outcome);

        Dictionary<int, string> names;
        await using (var db = _fixture.CreateDbContext())
        {
            names = await db.Teams.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name);
            var decision = await db.AssignmentDecisions.AsNoTracking().Include(d => d.Candidates)
                .SingleAsync(d => d.Id == stale.DecisionId);

            var staleNames = decision.Candidates
                .Where(c => c.EliminationReason == EliminationReason.StaleLocation)
                .Select(c => names[c.TeamId])
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(new[] { "Electrical Team A", "Electrical Team B", "Electrical Team E" }, staleNames);
        }

        Assert.Equal(24, await RefreshAsync(later, Config(true)));

        var fresh = await RecommendAsync(jobId, later);
        Assert.Equal(RecommendationOutcome.Recommended, fresh.Outcome);

        await using var verifyDb = _fixture.CreateDbContext();
        var freshDecision = await verifyDb.AssignmentDecisions.AsNoTracking().Include(d => d.Candidates)
            .SingleAsync(d => d.Id == fresh.DecisionId);
        Assert.Equal("Electrical Team A", names[freshDecision.SelectedCandidate!.TeamId]);
        Assert.DoesNotContain(freshDecision.Candidates, c => c.EliminationReason == EliminationReason.StaleLocation);
    }
}