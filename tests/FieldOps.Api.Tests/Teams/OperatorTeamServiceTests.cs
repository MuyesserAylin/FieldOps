using FieldOps.Api.Domain;
using FieldOps.Api.Teams;
using FieldOps.Api.Tests.Database;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests.Teams;

[Collection("Database")]
[Trait("Category", "Integration")]
public class OperatorTeamServiceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private const int GasSkillId = 1;
    private const int ElectricalSkillId = 3;

    private readonly PostgisFixture _fixture;

    public OperatorTeamServiceTests(PostgisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Team CreateTeam(string name, double longitude = 30.5206, double latitude = 39.7767) =>
        new(name, new Point(longitude, latitude) { SRID = 4326 }, Now.AddSeconds(-10));

    private async Task SaveAsync(Team team)
    {
        await using var db = _fixture.CreateDbContext();
        db.Teams.Add(team);
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<OperatorTeamItem>> ListAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var service = new OperatorTeamService(db, new FakeClock(Now));
        return await service.ListAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ListAsync_ReturnsLocationStatusAndSkills_OrderedById()
    {
        var zeta = CreateTeam("Zeta", longitude: 30.45, latitude: 39.80);
        zeta.AddSkill(GasSkillId, TeamSkillLevel.Basic);
        zeta.AddSkill(ElectricalSkillId, TeamSkillLevel.Expert);
        zeta.TransitionTo(TeamStatus.OfferPending);
        await SaveAsync(zeta);

        var alfa = CreateTeam("Alfa");
        alfa.AddSkill(GasSkillId, TeamSkillLevel.Intermediate);
        await SaveAsync(alfa);

        var items = await ListAsync();

        Assert.Equal(new[] { "Zeta", "Alfa" }, items.Select(i => i.Name));

        var first = items[0];
        Assert.Equal(39.80, first.Latitude, 6);
        Assert.Equal(30.45, first.Longitude, 6);
        Assert.Equal("OfferPending", first.Status);
        Assert.Equal(Now.AddSeconds(-10), first.LocationUpdatedAt);

        Assert.Equal(2, first.Skills.Count);
        Assert.Equal("Electrical", first.Skills[0].Name);
        Assert.Equal(3, first.Skills[0].Level);
        Assert.Equal("Gas", first.Skills[1].Name);
        Assert.Equal(1, first.Skills[1].Level);

        var second = items[1];
        Assert.Equal("Idle", second.Status);
        Assert.Single(second.Skills);
        Assert.Equal(2, second.Skills[0].Level);
    }

    [Theory]
    [InlineData(-60, 60, true)]
    [InlineData(-720, -480, false)]
    [InlineData(60, 480, false)]
    [InlineData(-480, 0, false)]
    [InlineData(0, 480, true)]
    public async Task OnShift_FollowsStartInclusiveEndExclusiveRule(int startMinutes, int endMinutes, bool expected)
    {
        var team = CreateTeam("Team A");
        team.AddShift(Now.AddMinutes(startMinutes), Now.AddMinutes(endMinutes));
        await SaveAsync(team);

        var items = await ListAsync();

        Assert.Equal(expected, Assert.Single(items).OnShift);
    }

    [Fact]
    public async Task OnShift_IsFalse_WhenTeamHasNoShift()
    {
        await SaveAsync(CreateTeam("Team A"));

        var items = await ListAsync();

        Assert.False(Assert.Single(items).OnShift);
    }
}