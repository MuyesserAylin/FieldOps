using FieldOps.Api.Assignment;
using FieldOps.Api.Domain;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Npgsql;

namespace FieldOps.Api.Tests.Database;

[Collection("Database")]
[Trait("Category", "Integration")]
public class AssignmentDecisionDatabaseTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly ScoreBreakdown SampleScore = new(75, 100, 66.67, 90, 82.5);

    private readonly PostgisFixture _fixture;

    public AssignmentDecisionDatabaseTests(PostgisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<long> SaveJobAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var faultType = await db.FaultTypes.SingleAsync(f => f.Name == "Gas leak");
        var location = new Point(32.8597, 39.9334) { SRID = 4326 };
        var job = new Job(faultType, location, "Smell of gas", "DECISIONTEST1", Now);

        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    private async Task<int> SaveTeamAsync(string name)
    {
        await using var db = _fixture.CreateDbContext();
        var team = new Team(name, new Point(30.5206, 39.7767) { SRID = 4326 }, Now);

        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team.Id;
    }

    private async Task<long> SaveDecisionAsync(long jobId, Action<AssignmentDecision>? fill = null)
    {
        await using var db = _fixture.CreateDbContext();
        var decision = new AssignmentDecision(jobId, Urgency.Low, WeightSet.Normal, Now);
        fill?.Invoke(decision);

        db.AssignmentDecisions.Add(decision);
        await db.SaveChangesAsync();
        return decision.Id;
    }

    [Fact]
    public async Task SavedDecision_ReadBackWithNewContext_KeepsHeaderAndRows()
    {
        var jobId = await SaveJobAsync();
        var teamA = await SaveTeamAsync("Team A");
        var teamB = await SaveTeamAsync("Team B");
        var decisionId = await SaveDecisionAsync(jobId, d =>
        {
            d.AddCandidate(teamA, 1.0, SampleScore, 1);
            d.AddEliminated(teamB, 6.2, EliminationReason.TooFar);
        });

        await using var db = _fixture.CreateDbContext();
        var loaded = await db.AssignmentDecisions
            .Include(d => d.Candidates)
            .SingleAsync(d => d.Id == decisionId);

        Assert.Equal(jobId, loaded.JobId);
        Assert.Equal(Urgency.Low, loaded.Urgency);
        Assert.Equal(WeightSet.Normal, loaded.WeightSet);
        Assert.Equal(Now, loaded.DecidedAt);
        Assert.Equal(2, loaded.Candidates.Count);

        var selected = loaded.SelectedCandidate;
        Assert.NotNull(selected);
        Assert.Equal(teamA, selected.TeamId);
        Assert.Equal(1, selected.Rank);
        Assert.False(selected.IsEliminated);
        Assert.Equal(1.0, selected.DistanceKm, 6);
        Assert.Equal(75.0, selected.DistanceScore!.Value, 6);
        Assert.Equal(100.0, selected.WorkloadScore!.Value, 6);
        Assert.Equal(66.67, selected.SkillScore!.Value, 6);
        Assert.Equal(90.0, selected.ResponseScore!.Value, 6);
        Assert.Equal(82.5, selected.TotalScore!.Value, 6);

        var eliminated = Assert.Single(loaded.Candidates, c => c.IsEliminated);
        Assert.Equal(teamB, eliminated.TeamId);
        Assert.Equal(6.2, eliminated.DistanceKm, 6);
        Assert.Equal(EliminationReason.TooFar, eliminated.EliminationReason);
        Assert.Null(eliminated.TotalScore);
        Assert.Null(eliminated.Rank);
    }

    [Fact]
    public async Task WeightSetAndEliminationReason_AreStoredAsText()
    {
        var jobId = await SaveJobAsync();
        var team = await SaveTeamAsync("Team A");
        var decisionId = await SaveDecisionAsync(jobId, d =>
            d.AddEliminated(team, 6.2, EliminationReason.TooFar));

        await using var db = _fixture.CreateDbContext();
        var weightSet = await db.Database
            .SqlQuery<string>($"SELECT weight_set AS \"Value\" FROM assignment_decisions WHERE id = {decisionId}")
            .SingleAsync();
        var reason = await db.Database
            .SqlQuery<string>($"SELECT elimination_reason AS \"Value\" FROM assignment_decision_candidates WHERE decision_id = {decisionId}")
            .SingleAsync();

        Assert.Equal("Normal", weightSet);
        Assert.Equal("TooFar", reason);
    }

    [Fact]
    public async Task TwoEliminatedTeams_CanBeSavedTogether_BecauseEmptyRanksDoNotCollide()
    {
        var jobId = await SaveJobAsync();
        var teamA = await SaveTeamAsync("Team A");
        var teamB = await SaveTeamAsync("Team B");

        var decisionId = await SaveDecisionAsync(jobId, d =>
        {
            d.AddEliminated(teamA, 6.2, EliminationReason.TooFar);
            d.AddEliminated(teamB, 2.0, EliminationReason.MissingSkill);
        });

        await using var db = _fixture.CreateDbContext();
        var count = await db.AssignmentDecisionCandidates.CountAsync(c => c.DecisionId == decisionId);

        Assert.Equal(2, count);
    }


    [Fact]
    public async Task Database_RejectsRowWithBothEliminationReasonAndScore()
    {
        var jobId = await SaveJobAsync();
        var team = await SaveTeamAsync("Team A");
        var decisionId = await SaveDecisionAsync(jobId);

        await using var db = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync(
                $"INSERT INTO assignment_decision_candidates (decision_id, team_id, distance_km, elimination_reason, total_score) VALUES ({decisionId}, {team}, {1.0}, {"TooFar"}, {50.0})"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("ck_assignment_decision_candidates_state", ex.ConstraintName);
    }

    [Fact]
    public async Task Database_RejectsRowWithNeitherReasonNorScores()
    {
        var jobId = await SaveJobAsync();
        var team = await SaveTeamAsync("Team A");
        var decisionId = await SaveDecisionAsync(jobId);

        await using var db = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync(
                $"INSERT INTO assignment_decision_candidates (decision_id, team_id, distance_km) VALUES ({decisionId}, {team}, {1.0})"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("ck_assignment_decision_candidates_state", ex.ConstraintName);
    }

    [Fact]
    public async Task Database_RejectsRankZero()
    {
        var jobId = await SaveJobAsync();
        var team = await SaveTeamAsync("Team A");
        var decisionId = await SaveDecisionAsync(jobId);

        await using var db = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync(
                $"INSERT INTO assignment_decision_candidates (decision_id, team_id, distance_km, distance_score, workload_score, skill_score, response_score, total_score, rank) VALUES ({decisionId}, {team}, {1.0}, {50.0}, {50.0}, {50.0}, {50.0}, {50.0}, {0})"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("ck_assignment_decision_candidates_rank", ex.ConstraintName);
    }

    [Fact]
    public async Task Database_RejectsSameTeamTwiceInOneDecision()
    {
        var jobId = await SaveJobAsync();
        var team = await SaveTeamAsync("Team A");
        var decisionId = await SaveDecisionAsync(jobId, d => d.AddCandidate(team, 1.0, SampleScore, 1));

        await using var db = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync(
                $"INSERT INTO assignment_decision_candidates (decision_id, team_id, distance_km, elimination_reason) VALUES ({decisionId}, {team}, {1.0}, {"TooFar"})"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }

    [Fact]
    public async Task Database_RejectsTwoCandidatesWithRankOneInOneDecision()
    {
        var jobId = await SaveJobAsync();
        var teamA = await SaveTeamAsync("Team A");
        var teamB = await SaveTeamAsync("Team B");
        var decisionId = await SaveDecisionAsync(jobId, d => d.AddCandidate(teamA, 1.0, SampleScore, 1));

        await using var db = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync(
                $"INSERT INTO assignment_decision_candidates (decision_id, team_id, distance_km, distance_score, workload_score, skill_score, response_score, total_score, rank) VALUES ({decisionId}, {teamB}, {2.0}, {50.0}, {50.0}, {50.0}, {50.0}, {50.0}, {1})"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }

    [Fact]
    public async Task Database_RejectsDeletingJobThatHasADecision()
    {
        var jobId = await SaveJobAsync();
        await SaveDecisionAsync(jobId);

        await using var db = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM jobs WHERE id = {jobId}"));

        Assert.True(
            ex.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation,
            $"Beklenmeyen hata kodu: {ex.SqlState}");
    }

    [Fact]
    public async Task Database_RejectsDeletingTeamThatIsInADecision()
    {
        var jobId = await SaveJobAsync();
        var team = await SaveTeamAsync("Team A");
        await SaveDecisionAsync(jobId, d => d.AddCandidate(team, 1.0, SampleScore, 1));

        await using var db = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM teams WHERE id = {team}"));

        Assert.True(
            ex.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation,
            $"Beklenmeyen hata kodu: {ex.SqlState}");
    }

    [Fact]
    public async Task DeletingADecision_AlsoDeletesItsRows()
    {
        var jobId = await SaveJobAsync();
        var teamA = await SaveTeamAsync("Team A");
        var teamB = await SaveTeamAsync("Team B");
        var decisionId = await SaveDecisionAsync(jobId, d =>
        {
            d.AddCandidate(teamA, 1.0, SampleScore, 1);
            d.AddEliminated(teamB, 6.2, EliminationReason.TooFar);
        });

        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"DELETE FROM assignment_decisions WHERE id = {decisionId}");
        var remainingRows = await db.AssignmentDecisionCandidates.CountAsync();

        Assert.Equal(0, remainingRows);
    }
}