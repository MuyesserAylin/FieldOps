using FieldOps.Api.Assignment;
using FieldOps.Api.Domain;
using FieldOps.Api.Teams;
using FieldOps.Api.Tests.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests.Assignment;

// Ekip öneri servisini gerçek PostgreSQL'de uçtan uca dener: yükleyici -> eleme -> puanlama -> karar günlüğü.
// Önce kendi kurduğumuz küçük veriyle (elle hesaplanabilir), sonra seeder'ın gerçek 24 ekibiyle.
[Collection("Database")]
[Trait("Category", "Integration")]
public class TeamRecommendationServiceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private const int GasSkillId = 1;
    private const int PlumbingSkillId = 2;
    private const int ElectricalSkillId = 3;

    private static readonly Point JobLocation =
        new(ServiceArea.CenterLongitude, ServiceArea.CenterLatitude) { SRID = 4326 };

    private readonly PostgisFixture _fixture;

    public TeamRecommendationServiceTests(PostgisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;


    private static double KmNorth(double km) => km * 0.009;

    private async Task<int> SaveTeamAsync(
        string name, double kmNorth, bool onShift, params (int SkillId, TeamSkillLevel Level)[] skills)
    {
        var location = new Point(ServiceArea.CenterLongitude, ServiceArea.CenterLatitude + KmNorth(kmNorth)) { SRID = 4326 };
        var team = new Team(name, location, Now.AddSeconds(-10));

        foreach (var (skillId, level) in skills)
        {
            team.AddSkill(skillId, level);
        }

        if (onShift)
        {
            team.AddShift(Now.AddHours(-4), Now.AddHours(4));
        }
        else
        {
            team.AddShift(Now.AddHours(5), Now.AddHours(13));
        }

        await using var db = _fixture.CreateDbContext();
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team.Id;
    }

    private async Task<long> SaveJobAsync(
        string faultTypeName, int createdMinutesAgo = 0, JobStatus status = JobStatus.Queued)
    {
        var createdAt = Now.AddMinutes(-createdMinutesAgo);

        await using var db = _fixture.CreateDbContext();
        var faultType = await db.FaultTypes.SingleAsync(f => f.Name == faultTypeName);
        var job = new Job(faultType, JobLocation, "Recommendation test", Guid.NewGuid().ToString("N")[..16], createdAt);

        if (status == JobStatus.Offered)
        {
            job.TransitionTo(JobStatus.Offered, createdAt);
        }

        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    private async Task SaveAssignedJobAsync(string faultTypeName, int teamId, DateTimeOffset assignedAt)
    {
        await using var db = _fixture.CreateDbContext();
        var faultType = await db.FaultTypes.SingleAsync(f => f.Name == faultTypeName);
        var job = new Job(faultType, JobLocation, "Assigned earlier", Guid.NewGuid().ToString("N")[..16], assignedAt);

        job.TransitionTo(JobStatus.Offered, assignedAt);
        job.TransitionTo(JobStatus.OnRoute, assignedAt);
        job.AssignTo(teamId, assignedAt);

        db.Jobs.Add(job);
        await db.SaveChangesAsync();
    }

    private async Task<RecommendationResult> RecommendAsync(long jobId)
    {
        await using var db = _fixture.CreateDbContext();
        var settings = Options.Create(TestSettings.Standard());

        var service = new TeamRecommendationService(
            db,
            new JobRequestLoader(db, settings),
            new TeamSnapshotLoader(db),
            new ScoringTeamLoader(db),
            new FakeClock(Now),
            settings);

        return await service.RecommendAsync(jobId, CancellationToken.None);
    }

    private async Task<AssignmentDecision> LoadDecisionAsync(long decisionId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.AssignmentDecisions
            .AsNoTracking()
            .Include(d => d.Candidates)
            .SingleAsync(d => d.Id == decisionId);
    }

    private static void AssertTotalMatchesWeights(AssignmentDecisionCandidate row, ScoringWeights weights)
    {
        var expected = (weights.Distance * row.DistanceScore!.Value
                        + weights.Workload * row.WorkloadScore!.Value
                        + weights.Skill * row.SkillScore!.Value
                        + weights.Response * row.ResponseScore!.Value) / 100;

        Assert.Equal(expected, row.TotalScore!.Value, 6);
    }

    private static List<AssignmentDecisionCandidate> CandidatesInRankOrder(AssignmentDecision decision)
        => decision.Candidates.Where(c => !c.IsEliminated).OrderBy(c => c.Rank).ToList();


    [Fact]
    public async Task LowJob_RecommendsTheBestTeam_AndLogsEveryTeamWithItsReason()
    {
        var near = await SaveTeamAsync("Near", 1, onShift: true, (ElectricalSkillId, TeamSkillLevel.Intermediate));
        var far = await SaveTeamAsync("Far", 3, onShift: true, (ElectricalSkillId, TeamSkillLevel.Expert));
        var gasOnly = await SaveTeamAsync("GasOnly", 1, onShift: true, (GasSkillId, TeamSkillLevel.Expert));
        var tooFar = await SaveTeamAsync("TooFar", 6, onShift: true, (ElectricalSkillId, TeamSkillLevel.Expert));
        var jobId = await SaveJobAsync("Street light failure");

        var result = await RecommendAsync(jobId);

        Assert.Equal(RecommendationOutcome.Recommended, result.Outcome);
        Assert.Equal(2, result.CandidateCount);
        Assert.Equal(2, result.EliminatedCount);

        var decision = await LoadDecisionAsync(result.DecisionId!.Value);
        Assert.Equal(jobId, decision.JobId);
        Assert.Equal(Urgency.Low, decision.Urgency);
        Assert.Equal(WeightSet.Normal, decision.WeightSet);
        Assert.Equal(Now, decision.DecidedAt);
        Assert.Equal(4, decision.Candidates.Count);

        Assert.Equal(near, decision.SelectedCandidate!.TeamId);
        var ranked = CandidatesInRankOrder(decision);
        Assert.Equal(new[] { near, far }, ranked.Select(c => c.TeamId).ToArray());
        Assert.Equal(new[] { 1, 2 }, ranked.Select(c => c.Rank!.Value).ToArray());

        Assert.Equal(EliminationReason.MissingSkill, decision.Candidates.Single(c => c.TeamId == gasOnly).EliminationReason);
        Assert.Equal(EliminationReason.TooFar, decision.Candidates.Single(c => c.TeamId == tooFar).EliminationReason);

        Assert.InRange(ranked[0].DistanceKm, 0.95, 1.05);
        Assert.InRange(ranked[1].DistanceKm, 2.9, 3.1);

        foreach (var row in ranked)
        {
            Assert.Equal(100 * (1 - row.DistanceKm / 4.0), row.DistanceScore!.Value, 6);
            Assert.Equal(100.0, row.WorkloadScore!.Value, 6);
            Assert.Equal(100.0, row.ResponseScore!.Value, 6);
            AssertTotalMatchesWeights(row, TestSettings.Standard().NormalWeights);
        }

        Assert.Equal(100.0 * 2 / 3, ranked[0].SkillScore!.Value, 4);
        Assert.Equal(100.0, ranked[1].SkillScore!.Value, 4);
        Assert.True(ranked[0].TotalScore > ranked[1].TotalScore);
    }


    [Fact]
    public async Task HeavilyWorkedNearTeam_LosesToRestedFarTeam_OnANormalJob()
    {
        var near = await SaveTeamAsync("Near", 1, onShift: true, (ElectricalSkillId, TeamSkillLevel.Intermediate));
        var far = await SaveTeamAsync("Far", 3, onShift: true, (ElectricalSkillId, TeamSkillLevel.Expert));

        await SaveAssignedJobAsync("Burst water pipe", near, Now.AddHours(-2));
        await SaveAssignedJobAsync("Burst water pipe", near, Now.AddHours(-2));
        await SaveAssignedJobAsync("Pothole", near, Now.AddHours(-2));

        var jobId = await SaveJobAsync("Street light failure");

        var result = await RecommendAsync(jobId);

        var decision = await LoadDecisionAsync(result.DecisionId!.Value);
        var nearRow = decision.Candidates.Single(c => c.TeamId == near);
        var farRow = decision.Candidates.Single(c => c.TeamId == far);

        Assert.Equal(100 * (1 - 300.0 / 360), nearRow.WorkloadScore!.Value, 4);
        Assert.Equal(100.0, farRow.WorkloadScore!.Value, 4);
        Assert.Equal(far, decision.SelectedCandidate!.TeamId);
        Assert.Equal(1, farRow.Rank);
        Assert.Equal(2, nearRow.Rank);
    }


    [Fact]
    public async Task JobWaiting61Minutes_IsDecidedAsHigh_ButTheJobRowKeepsItsUrgency()
    {
        var near = await SaveTeamAsync("Near", 1, onShift: true, (ElectricalSkillId, TeamSkillLevel.Expert));
        var at6Km = await SaveTeamAsync("At6Km", 6, onShift: true, (ElectricalSkillId, TeamSkillLevel.Expert));
        var at15Km = await SaveTeamAsync("At15Km", 15, onShift: true, (ElectricalSkillId, TeamSkillLevel.Expert));
        var jobId = await SaveJobAsync("Street light failure", createdMinutesAgo: 61);

        var result = await RecommendAsync(jobId);

        // Yüksek: sınır 12 km. 6 km'deki ekip artık aday, 15 km'deki hâlâ elenir.
        Assert.Equal(2, result.CandidateCount);
        Assert.Equal(1, result.EliminatedCount);

        var decision = await LoadDecisionAsync(result.DecisionId!.Value);
        Assert.Equal(Urgency.High, decision.Urgency);
        Assert.Equal(WeightSet.Urgent, decision.WeightSet);
        Assert.Equal(EliminationReason.TooFar, decision.Candidates.Single(c => c.TeamId == at15Km).EliminationReason);
        Assert.Equal(new[] { near, at6Km }, CandidatesInRankOrder(decision).Select(c => c.TeamId).ToArray());

        foreach (var row in CandidatesInRankOrder(decision))
        {
            Assert.Equal(100 * (1 - row.DistanceKm / 12.0), row.DistanceScore!.Value, 6);
            AssertTotalMatchesWeights(row, TestSettings.Standard().UrgentWeights);
        }

        await using var db = _fixture.CreateDbContext();
        var job = await db.Jobs.AsNoTracking().SingleAsync(j => j.Id == jobId);
        Assert.Equal(Urgency.Low, job.Urgency);
        Assert.Equal(Urgency.Low, job.InitialUrgency);
    }


    [Fact]
    public async Task CriticalJob_HasNoDistanceLimit_AndScoresDistanceAgainstTheDiagonal()
    {
        await SaveTeamAsync("GasNear", 1, onShift: true, (GasSkillId, TeamSkillLevel.Expert));
        var far = await SaveTeamAsync("GasFar", 18, onShift: true, (GasSkillId, TeamSkillLevel.Expert));
        var jobId = await SaveJobAsync("Gas leak");

        var result = await RecommendAsync(jobId);

        Assert.Equal(2, result.CandidateCount);
        Assert.Equal(0, result.EliminatedCount);

        var decision = await LoadDecisionAsync(result.DecisionId!.Value);
        Assert.Equal(Urgency.Critical, decision.Urgency);
        Assert.Equal(WeightSet.Urgent, decision.WeightSet);

        var diagonalKm = TestSettings.Standard().GetDistanceReferenceKm(Urgency.Critical);
        var farRow = decision.Candidates.Single(c => c.TeamId == far);
        Assert.Equal(100 * (1 - farRow.DistanceKm / diagonalKm), farRow.DistanceScore!.Value, 6);
        Assert.InRange(farRow.DistanceKm, 17.5, 18.5);
    }


    [Fact]
    public async Task WhenEveryTeamIsEliminated_ReturnsNoCandidates_AndStillLogsTheDecision()
    {
        var gasOnly = await SaveTeamAsync("GasOnly", 1, onShift: true, (GasSkillId, TeamSkillLevel.Expert));
        var offShift = await SaveTeamAsync("OffShift", 1, onShift: false, (ElectricalSkillId, TeamSkillLevel.Expert));
        var jobId = await SaveJobAsync("Street light failure");

        var result = await RecommendAsync(jobId);

        Assert.Equal(RecommendationOutcome.NoCandidates, result.Outcome);
        Assert.Equal(0, result.CandidateCount);
        Assert.Equal(2, result.EliminatedCount);
        Assert.NotNull(result.DecisionId);

        var decision = await LoadDecisionAsync(result.DecisionId!.Value);
        Assert.Null(decision.SelectedCandidate);
        Assert.Equal(EliminationReason.MissingSkill, decision.Candidates.Single(c => c.TeamId == gasOnly).EliminationReason);
        Assert.Equal(EliminationReason.NotOnShift, decision.Candidates.Single(c => c.TeamId == offShift).EliminationReason);
    }


    [Fact]
    public async Task MissingJob_ReturnsJobNotFound_AndWritesNoDecision()
    {
        var result = await RecommendAsync(999_999);

        Assert.Equal(RecommendationOutcome.JobNotFound, result.Outcome);
        Assert.Null(result.DecisionId);
        Assert.Equal(0, result.CandidateCount);
        Assert.Equal(0, result.EliminatedCount);
        await AssertNoDecisionsAsync();
    }

    [Fact]
    public async Task JobNotInQueue_ReturnsJobNotQueued_AndWritesNoDecision()
    {
        await SaveTeamAsync("Near", 1, onShift: true, (ElectricalSkillId, TeamSkillLevel.Expert));
        var jobId = await SaveJobAsync("Street light failure", status: JobStatus.Offered);

        var result = await RecommendAsync(jobId);

        Assert.Equal(RecommendationOutcome.JobNotQueued, result.Outcome);
        Assert.Null(result.DecisionId);
        await AssertNoDecisionsAsync();
    }

    private async Task AssertNoDecisionsAsync()
    {
        await using var db = _fixture.CreateDbContext();
        Assert.Equal(0, await db.AssignmentDecisions.CountAsync());
        Assert.Equal(0, await db.AssignmentDecisionCandidates.CountAsync());
    }


    private async Task<(List<string> Jobs, List<string> Teams)> ReadStateAsync()
    {
        await using var db = _fixture.CreateDbContext();

        var jobs = await db.Jobs.AsNoTracking().OrderBy(j => j.Id)
            .Select(j => new { j.Id, j.Status, j.Urgency, j.InitialUrgency, j.AssignedTeamId, j.AssignedAt, j.Version })
            .ToListAsync();
        var teams = await db.Teams.AsNoTracking().OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.Status, t.LocationUpdatedAt, t.CooldownUntil })
            .ToListAsync();

        return (jobs.Select(j => j.ToString()!).ToList(), teams.Select(t => t.ToString()!).ToList());
    }

    [Fact]
    public async Task Recommendation_ChangesNoJobOrTeam_AndEveryCallWritesANewDecision()
    {
        await SaveTeamAsync("Near", 1, onShift: true, (ElectricalSkillId, TeamSkillLevel.Expert));
        var jobId = await SaveJobAsync("Street light failure", createdMinutesAgo: 61);

        var before = await ReadStateAsync();
        var first = await RecommendAsync(jobId);
        var second = await RecommendAsync(jobId);
        var after = await ReadStateAsync();

        Assert.Equal(before.Jobs, after.Jobs);
        Assert.Equal(before.Teams, after.Teams);

        Assert.NotEqual(first.DecisionId, second.DecisionId);
        await using var db = _fixture.CreateDbContext();
        Assert.Equal(2, await db.AssignmentDecisions.CountAsync(d => d.JobId == jobId));
    }


    private async Task<Dictionary<int, string>> SeedAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DemoTeams"] = "true" })
            .Build();

        await using var db = _fixture.CreateDbContext();

        var seeded = await new DemoTeamSeeder(db, configuration, new FakeClock(Now))
            .SeedAsync(CancellationToken.None);
        Assert.True(seeded);

        return await db.Teams.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name);
    }

    private static string[] NamesWithReason(
        AssignmentDecision decision, Dictionary<int, string> names, EliminationReason reason)
        => decision.Candidates
            .Where(c => c.EliminationReason == reason)
            .Select(c => names[c.TeamId])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public async Task RealSeed_StreetLightInCityCenter_RecommendsElectricalTeamA()
    {
        var names = await SeedAsync();
        var jobId = await SaveJobAsync("Street light failure");

        var result = await RecommendAsync(jobId);

        Assert.Equal(RecommendationOutcome.Recommended, result.Outcome);
        Assert.Equal(1, result.CandidateCount);
        Assert.Equal(23, result.EliminatedCount);

        var decision = await LoadDecisionAsync(result.DecisionId!.Value);
        Assert.Equal(24, decision.Candidates.Count);
        Assert.Equal(Urgency.Low, decision.Urgency);
        Assert.Equal(WeightSet.Normal, decision.WeightSet);

        var selected = decision.SelectedCandidate!;
        Assert.Equal("Electrical Team A", names[selected.TeamId]);
        Assert.Equal(1, selected.Rank);
        Assert.InRange(selected.DistanceKm, 2.0, 2.5);
        Assert.Equal(100.0, selected.WorkloadScore!.Value, 6);  // seed ekiplerinin henüz işi yok
        Assert.Equal(100.0, selected.ResponseScore!.Value, 6);  // teklif geçmişi yok
        Assert.Equal(100 * (1 - selected.DistanceKm / 4.0), selected.DistanceScore!.Value, 6);
        AssertTotalMatchesWeights(selected, TestSettings.Standard().NormalWeights);

        Assert.Equal(
            new[] { "Electrical Team B", "Electrical Team E" },
            NamesWithReason(decision, names, EliminationReason.TooFar));
        Assert.Equal(
            new[] { "Electrical Team C", "Electrical Team D", "Electrical Team F" },
            NamesWithReason(decision, names, EliminationReason.NotOnShift));
        Assert.Equal(18, NamesWithReason(decision, names, EliminationReason.MissingSkill).Length);
    }

    [Fact]
    public async Task RealSeed_GasLeakInCityCenter_HasFourCandidatesAndNoDistanceElimination()
    {
        var names = await SeedAsync();
        var jobId = await SaveJobAsync("Gas leak");

        var result = await RecommendAsync(jobId);

        Assert.Equal(RecommendationOutcome.Recommended, result.Outcome);
        Assert.Equal(4, result.CandidateCount);
        Assert.Equal(20, result.EliminatedCount);

        var decision = await LoadDecisionAsync(result.DecisionId!.Value);
        Assert.Equal(Urgency.Critical, decision.Urgency);
        Assert.Equal(WeightSet.Urgent, decision.WeightSet);
        Assert.Empty(NamesWithReason(decision, names, EliminationReason.TooFar));

        var ranked = CandidatesInRankOrder(decision);
        Assert.Equal(
            new[] { "Electrical Team A", "Gas Team A", "Gas Team B", "Gas Team E" },
            ranked.Select(c => names[c.TeamId]).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { 1, 2, 3, 4 }, ranked.Select(c => c.Rank!.Value).ToArray());

        var totals = ranked.Select(c => c.TotalScore!.Value).ToList();
        Assert.Equal(totals.OrderByDescending(t => t).ToList(), totals);
        foreach (var row in ranked)
        {
            Assert.InRange(row.TotalScore!.Value, 0, 100);
            AssertTotalMatchesWeights(row, TestSettings.Standard().UrgentWeights);
        }
    }
}