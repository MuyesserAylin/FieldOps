using FieldOps.Api.Assignment;
using FieldOps.Api.Domain;
using FieldOps.Api.Tests.Database;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests.Assignment;

[Collection("Database")]
[Trait("Category", "Integration")]
public class ScoringTeamLoaderTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int hour, int minute = 0) =>
        new(2026, 11, 15, hour, minute, 0, TimeSpan.Zero);

    private const int PlumbingSkillId = 2;
    private const int ElectricalSkillId = 3;

    // Elektrik isteyen bir iş
    private static readonly JobRequest ElectricalJob =
        new(1, ElectricalSkillId, Urgency.Low, new HashSet<int>());

    private readonly PostgisFixture _fixture;

    public ScoringTeamLoaderTests(PostgisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<int> SaveTeamAsync(string name, params (int SkillId, TeamSkillLevel Level)[] skills)
    {
        var team = new Team(name, new Point(30.5206, 39.7767) { SRID = 4326 }, Now.AddSeconds(-10));
        foreach (var (skillId, level) in skills)
        {
            team.AddSkill(skillId, level);
        }

        await using var db = _fixture.CreateDbContext();
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team.Id;
    }

    private async Task SaveAssignedJobAsync(
        string faultTypeName, int teamId, DateTimeOffset assignedAt, bool cancelled = false)
    {
        await using var db = _fixture.CreateDbContext();
        var faultType = await db.FaultTypes.SingleAsync(f => f.Name == faultTypeName);
        var location = new Point(32.8597, 39.9334) { SRID = 4326 };
        var trackingCode = Guid.NewGuid().ToString("N")[..16];
        var job = new Job(faultType, location, "Loader test", trackingCode, Now.AddDays(-2));

        job.TransitionTo(JobStatus.Offered, assignedAt);
        job.TransitionTo(JobStatus.OnRoute, assignedAt);
        job.AssignTo(teamId, assignedAt);

        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        if (cancelled)
        {
            var cancelledStatus = JobStatus.Cancelled.ToString();
            await db.Database.ExecuteSqlAsync($"UPDATE jobs SET status = {cancelledStatus} WHERE id = {job.Id}");
        }
    }

    private static TeamSnapshot Snapshot(int teamId, double distanceKm, params ShiftWindow[] shifts) =>
        new(teamId, TeamStatus.Idle, distanceKm, Now.AddSeconds(-10), null, new[] { ElectricalSkillId }, shifts);

    private async Task<IReadOnlyList<ScoringTeam>> LoadAsync(params TeamSnapshot[] candidates)
    {
        await using var db = _fixture.CreateDbContext();
        return await new ScoringTeamLoader(db).LoadAsync(ElectricalJob, candidates, Now, CancellationToken.None);
    }

    [Fact]
    public async Task WorkedExample_SumsOnlyThisTeamsNonCancelledJobsInTheCoveringShift()
    {
        var teamId = await SaveTeamAsync("Team 3",
            (ElectricalSkillId, TeamSkillLevel.Expert),
            (PlumbingSkillId, TeamSkillLevel.Basic));
        var otherId = await SaveTeamAsync("Other", (ElectricalSkillId, TeamSkillLevel.Basic));

        await SaveAssignedJobAsync("Street light failure", teamId, At(9, 10));
        await SaveAssignedJobAsync("Burst water pipe", teamId, At(11));
        await SaveAssignedJobAsync("Pothole", teamId, At(14).AddDays(-1));
        await SaveAssignedJobAsync("Power outage", teamId, At(10), cancelled: true);
        await SaveAssignedJobAsync("Gas leak", otherId, At(12));

        var result = await LoadAsync(Snapshot(teamId, 1.0, new ShiftWindow(At(8), At(16))));

        var team = Assert.Single(result);
        Assert.Equal(teamId, team.TeamId);
        Assert.Equal(1.0, team.DistanceKm, 6);
        Assert.Equal(150.0, team.WorkedMinutes, 6);
        Assert.Equal(480.0, team.ShiftMinutes, 6);
        Assert.Equal(TeamSkillLevel.Expert, team.SkillLevel);
        Assert.Equal(0, team.OffersConsidered);
        Assert.Equal(0, team.OffersAccepted);
    }

    [Fact]
    public async Task TeamWithNoAssignedJobs_HasZeroWorkedMinutes()
    {
        var teamId = await SaveTeamAsync("Fresh", (ElectricalSkillId, TeamSkillLevel.Intermediate));

        var result = await LoadAsync(Snapshot(teamId, 3.0, new ShiftWindow(At(8), At(16))));

        var team = Assert.Single(result);
        Assert.Equal(0.0, team.WorkedMinutes, 6);
        Assert.Equal(TeamSkillLevel.Intermediate, team.SkillLevel);
    }

    [Fact]
    public async Task EachTeamIsMeasuredInItsOwnShift_AndInputOrderIsKept()
    {
        var teamA = await SaveTeamAsync("A", (ElectricalSkillId, TeamSkillLevel.Intermediate));
        var teamB = await SaveTeamAsync("B", (ElectricalSkillId, TeamSkillLevel.Expert));

        await SaveAssignedJobAsync("Street light failure", teamA, At(9, 10));
        await SaveAssignedJobAsync("Pothole", teamB, At(9, 10));
        await SaveAssignedJobAsync("Burst water pipe", teamB, At(11, 30));

        // Sıra: önce B, sonra A
        var result = await LoadAsync(
            Snapshot(teamB, 2.0, new ShiftWindow(At(11), At(19))),
            Snapshot(teamA, 1.0, new ShiftWindow(At(8), At(16))));

        Assert.Equal(new[] { teamB, teamA }, result.Select(t => t.TeamId).ToArray());
        Assert.Equal(120.0, result[0].WorkedMinutes, 6);
        Assert.Equal(30.0, result[1].WorkedMinutes, 6);
    }

    [Fact]
    public async Task ShiftStartIsIncluded_AndShiftEndIsExcluded()
    {
        var teamId = await SaveTeamAsync("Boundary", (ElectricalSkillId, TeamSkillLevel.Expert));

        await SaveAssignedJobAsync("Street light failure", teamId, At(8));
        await SaveAssignedJobAsync("Pothole", teamId, At(16));

        var result = await LoadAsync(Snapshot(teamId, 1.0, new ShiftWindow(At(8), At(16))));

        Assert.Equal(30.0, Assert.Single(result).WorkedMinutes, 6);
    }

    [Fact]
    public async Task WhenTwoShiftsCoverNow_TheLatestStartingOneIsUsed()
    {
        var teamId = await SaveTeamAsync("Overlap", (ElectricalSkillId, TeamSkillLevel.Expert));

        await SaveAssignedJobAsync("Pothole", teamId, At(9));
        await SaveAssignedJobAsync("Street light failure", teamId, At(11));

        var result = await LoadAsync(Snapshot(
            teamId, 1.0,
            new ShiftWindow(At(4), At(14)),
            new ShiftWindow(At(10), At(18))));

        var team = Assert.Single(result);
        Assert.Equal(480.0, team.ShiftMinutes, 6);
        Assert.Equal(30.0, team.WorkedMinutes, 6);
    }

    [Fact]
    public async Task NoCandidates_ReturnsEmptyList()
    {
        Assert.Empty(await LoadAsync());
    }

    [Fact]
    public async Task CandidateNotInShift_ThrowsBecauseEliminationShouldHaveRemovedIt()
    {
        var teamId = await SaveTeamAsync("Late", (ElectricalSkillId, TeamSkillLevel.Expert));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LoadAsync(Snapshot(teamId, 1.0, new ShiftWindow(At(13), At(21)))));
    }

    [Fact]
    public async Task CandidateWithoutTheRequiredSkill_Throws()
    {
        var teamId = await SaveTeamAsync("Plumber", (PlumbingSkillId, TeamSkillLevel.Expert));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LoadAsync(Snapshot(teamId, 1.0, new ShiftWindow(At(8), At(16)))));
    }

    [Fact]
    public async Task SameCandidateTwice_Throws()
    {
        var teamId = await SaveTeamAsync("Twin", (ElectricalSkillId, TeamSkillLevel.Expert));
        var shift = new ShiftWindow(At(8), At(16));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            LoadAsync(Snapshot(teamId, 1.0, shift), Snapshot(teamId, 1.0, shift)));
    }
}