using FieldOps.Api.Assignment;
using FieldOps.Api.Domain;
using FieldOps.Api.Tests.Database;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests.Assignment;

[Collection("Database")]
[Trait("Category", "Integration")]
public class TeamSnapshotLoaderTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private const int GasSkillId = 1;
    private const int ElectricalSkillId = 3;

    private static readonly Point JobLocation =
        new(ServiceArea.CenterLongitude, ServiceArea.CenterLatitude) { SRID = 4326 };

    private readonly PostgisFixture _fixture;

    public TeamSnapshotLoaderTests(PostgisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Team CreateTeam(string name, double longitude, double latitude) =>
        new(name, new Point(longitude, latitude) { SRID = 4326 }, Now.AddSeconds(-10));

    private async Task SaveAsync(Team team)
    {
        await using var db = _fixture.CreateDbContext();
        db.Teams.Add(team);
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<TeamSnapshot>> LoadAsync()
    {
        await using var db = _fixture.CreateDbContext();
        return await new TeamSnapshotLoader(db).LoadAsync(JobLocation, Now, CancellationToken.None);
    }

    [Fact]
    public async Task Snapshot_carries_team_data_and_distance_in_km_ordered_by_id()
    {
        var zeta = CreateTeam("Zeta", ServiceArea.CenterLongitude, ServiceArea.CenterLatitude);
        zeta.AddSkill(GasSkillId, TeamSkillLevel.Expert);
        await SaveAsync(zeta);

        var alfa = CreateTeam("Alfa", ServiceArea.CenterLongitude, ServiceArea.CenterLatitude + 0.018);
        alfa.AddSkill(ElectricalSkillId, TeamSkillLevel.Basic);
        alfa.AddSkill(GasSkillId, TeamSkillLevel.Intermediate);
        alfa.AddShift(Now.AddHours(-1), Now.AddHours(7));
        alfa.TransitionTo(TeamStatus.OfferPending);
        await SaveAsync(alfa);

        var snapshots = await LoadAsync();

        Assert.Equal(2, snapshots.Count);

        var first = snapshots[0];
        Assert.InRange(first.DistanceKm, 0, 0.001);
        Assert.Equal(TeamStatus.Idle, first.Status);
        Assert.Equal(new[] { GasSkillId }, first.SkillIds.OrderBy(id => id));
        Assert.Empty(first.Shifts);
        Assert.Null(first.CooldownUntil);

        var second = snapshots[1];
        Assert.InRange(second.DistanceKm, 1.9, 2.1);
        Assert.Equal(TeamStatus.OfferPending, second.Status);
        Assert.Equal(new[] { GasSkillId, ElectricalSkillId }, second.SkillIds.OrderBy(id => id));
        Assert.Equal(Now.AddSeconds(-10), second.LocationUpdatedAt);

        var shift = Assert.Single(second.Shifts);
        Assert.Equal(Now.AddHours(-1), shift.StartsAt);
        Assert.Equal(Now.AddHours(7), shift.EndsAt);
    }

    [Fact]
    public async Task Shifts_ending_at_or_before_now_are_not_loaded()
    {
        var team = CreateTeam("Team", ServiceArea.CenterLongitude, ServiceArea.CenterLatitude);
        team.AddShift(Now.AddHours(-20), Now.AddHours(-8));
        team.AddShift(Now.AddHours(-8), Now);
        team.AddShift(Now.AddHours(-1), Now.AddHours(7));
        team.AddShift(Now.AddHours(10), Now.AddHours(22));
        await SaveAsync(team);

        var snapshot = Assert.Single(await LoadAsync());

        Assert.Equal(
            new[] { Now.AddHours(-1), Now.AddHours(10) },
            snapshot.Shifts.Select(s => s.StartsAt).OrderBy(t => t));
    }

    [Fact]
    public async Task Cooldown_is_carried_to_snapshot()
    {
        var cooldownUntil = Now.AddMinutes(3);
        var team = CreateTeam("Team", ServiceArea.CenterLongitude, ServiceArea.CenterLatitude);

        await using (var db = _fixture.CreateDbContext())
        {
            db.Teams.Add(team);
            db.Entry(team).Property(t => t.CooldownUntil).CurrentValue = cooldownUntil;
            await db.SaveChangesAsync();
        }

        var snapshot = Assert.Single(await LoadAsync());

        Assert.Equal(cooldownUntil, snapshot.CooldownUntil);
    }

    [Fact]
    public async Task No_teams_gives_empty_list()
    {
        Assert.Empty(await LoadAsync());
    }
}