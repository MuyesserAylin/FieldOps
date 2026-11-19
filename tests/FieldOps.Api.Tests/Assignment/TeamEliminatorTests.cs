using FieldOps.Api.Assignment;
using FieldOps.Api.Domain;
using static FieldOps.Api.Tests.Assignment.EliminationTestData;

namespace FieldOps.Api.Tests.Assignment;

public class TeamEliminatorTests
{
    private static EliminationResult Run(params TeamSnapshot[] teams)
        => TeamEliminator.Eliminate(Job(), teams, Now, TestSettings.Standard());

    private static void AssertEliminatedWith(EliminationResult result, int teamId, EliminationReason reason)
    {
        Assert.Empty(result.Candidates);
        var eliminated = Assert.Single(result.Eliminated);
        Assert.Equal(teamId, eliminated.TeamId);
        Assert.Equal(reason, eliminated.Reason);
    }


    [Fact]
    public void Team_with_required_skill_is_candidate()
    {
        var result = Run(Team(id: 1));

        Assert.Equal(new[] { 1 }, result.Candidates.Select(t => t.TeamId));
        Assert.Empty(result.Eliminated);
    }

    [Fact]
    public void Team_without_required_skill_is_eliminated_with_MissingSkill()
    {
        var result = Run(Team(id: 1, skillIds: new[] { 2 }));

        AssertEliminatedWith(result, 1, EliminationReason.MissingSkill);
    }

    [Fact]
    public void Team_with_extra_skills_is_still_candidate()
    {
        var result = Run(Team(id: 1, skillIds: new[] { 1, ElectricalSkillId }));

        Assert.Single(result.Candidates);
    }

    [Fact]
    public void Result_keeps_input_order()
    {
        var result = Run(
            Team(id: 5),
            Team(id: 2, skillIds: new[] { 2 }),
            Team(id: 9));

        Assert.Equal(new[] { 5, 9 }, result.Candidates.Select(t => t.TeamId));
        Assert.Equal(new[] { 2 }, result.Eliminated.Select(e => e.TeamId));
    }

    [Fact]
    public void Empty_team_list_gives_empty_result()
    {
        var result = Run();

        Assert.Empty(result.Candidates);
        Assert.Empty(result.Eliminated);
    }


    [Theory]
    [InlineData(-60, 60, true)]
    [InlineData(-720, -480, false)]
    [InlineData(60, 480, false)]
    [InlineData(-480, 0, false)]
    [InlineData(0, 480, true)]
    public void Shift_boundaries_are_start_inclusive_end_exclusive(int startMinutes, int endMinutes, bool isCandidate)
    {
        var shift = new ShiftWindow(Now.AddMinutes(startMinutes), Now.AddMinutes(endMinutes));

        var result = Run(Team(id: 1, shifts: new[] { shift }));

        if (isCandidate)
        {
            Assert.Single(result.Candidates);
        }
        else
        {
            AssertEliminatedWith(result, 1, EliminationReason.NotOnShift);
        }
    }

    [Fact]
    public void Team_without_any_shift_is_eliminated_with_NotOnShift()
    {
        var result = Run(Team(id: 1, shifts: Array.Empty<ShiftWindow>()));

        AssertEliminatedWith(result, 1, EliminationReason.NotOnShift);
    }

    [Fact]
    public void Team_with_one_covering_shift_among_many_is_candidate()
    {
        var shifts = new[]
        {
            new ShiftWindow(Now.AddHours(-30), Now.AddHours(-18)),
            new ShiftWindow(Now.AddHours(-1), Now.AddHours(11)),
            new ShiftWindow(Now.AddHours(18), Now.AddHours(30))
        };

        var result = Run(Team(id: 1, shifts: shifts));

        Assert.Single(result.Candidates);
    }


    [Theory]
    [InlineData(TeamStatus.OfferPending)]
    [InlineData(TeamStatus.OnRoute)]
    [InlineData(TeamStatus.OnSite)]
    [InlineData(TeamStatus.Offline)]
    public void Team_that_is_not_idle_is_eliminated_with_NotIdle(TeamStatus status)
    {
        var result = Run(Team(id: 1, status: status));

        AssertEliminatedWith(result, 1, EliminationReason.NotIdle);
    }

    [Fact]
    public void Critical_job_does_not_relax_the_idle_rule()
    {
        var result = TeamEliminator.Eliminate(
            Job(urgency: Urgency.Critical),
            new[] { Team(id: 1, status: TeamStatus.OnRoute) },
            Now,
            TestSettings.Standard());

        AssertEliminatedWith(result, 1, EliminationReason.NotIdle);
    }


    [Fact]
    public void Skill_rule_comes_before_shift_rule()
    {
        var result = Run(Team(id: 1, skillIds: new[] { 2 }, shifts: Array.Empty<ShiftWindow>()));

        AssertEliminatedWith(result, 1, EliminationReason.MissingSkill);
    }

    [Fact]
    public void Shift_rule_comes_before_idle_rule()
    {
        var result = Run(Team(id: 1, status: TeamStatus.OnRoute, shifts: Array.Empty<ShiftWindow>()));

        AssertEliminatedWith(result, 1, EliminationReason.NotOnShift);
    }
    // ---- Kural 4: konum bayat mı (60 sn; kesin olarak eskiyse bayat) ----

    [Theory]
    [InlineData(0, true)]
    [InlineData(30, true)]
    [InlineData(60, true)]    // tam sınır: hâlâ taze
    [InlineData(61, false)]   // 1 saniye fazla: bayat
    [InlineData(600, false)]
    public void Location_staleness_boundary_is_60_seconds(int ageSeconds, bool isCandidate)
    {
        var result = Run(Team(id: 1, locationUpdatedAt: Now.AddSeconds(-ageSeconds)));

        if (isCandidate)
        {
            Assert.Single(result.Candidates);
        }
        else
        {
            AssertEliminatedWith(result, 1, EliminationReason.StaleLocation);
        }
    }

    [Fact]
    public void Location_time_in_the_future_is_not_stale()
    {
        var result = Run(Team(id: 1, locationUpdatedAt: Now.AddSeconds(30)));

        Assert.Single(result.Candidates);
    }

    [Fact]
    public void Staleness_limit_comes_from_settings()
    {
        var settings = TestSettings.Standard();
        settings.LocationMaxAgeSeconds = 10;

        var result = TeamEliminator.Eliminate(
            Job(),
            new[] { Team(id: 1, locationUpdatedAt: Now.AddSeconds(-30)) },
            Now,
            settings);

        AssertEliminatedWith(result, 1, EliminationReason.StaleLocation);
    }

    [Fact]
    public void Idle_rule_comes_before_stale_location_rule()
    {
        var result = Run(Team(
            id: 1,
            status: TeamStatus.OnRoute,
            locationUpdatedAt: Now.AddMinutes(-10)));

        AssertEliminatedWith(result, 1, EliminationReason.NotIdle);
    }


    [Theory]
    [InlineData(300, false)]
    [InlineData(1, false)]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    public void Cooldown_ends_exactly_at_CooldownUntil(int secondsFromNow, bool isCandidate)
    {
        var result = Run(Team(id: 1, cooldownUntil: Now.AddSeconds(secondsFromNow)));

        if (isCandidate)
        {
            Assert.Single(result.Candidates);
        }
        else
        {
            AssertEliminatedWith(result, 1, EliminationReason.CoolingDown);
        }
    }

    [Fact]
    public void Stale_location_rule_comes_before_cooldown_rule()
    {
        var result = Run(Team(
            id: 1,
            locationUpdatedAt: Now.AddMinutes(-10),
            cooldownUntil: Now.AddMinutes(5)));

        AssertEliminatedWith(result, 1, EliminationReason.StaleLocation);
    }

    private static EliminationResult RunJob(JobRequest job, params TeamSnapshot[] teams)
    => TeamEliminator.Eliminate(job, teams, Now, TestSettings.Standard());


    [Theory]
    [InlineData(Urgency.Low, 4.0, true)]
    [InlineData(Urgency.Low, 4.01, false)]
    [InlineData(Urgency.Medium, 8.0, true)]
    [InlineData(Urgency.Medium, 8.01, false)]
    [InlineData(Urgency.High, 12.0, true)]
    [InlineData(Urgency.High, 12.01, false)]
    [InlineData(Urgency.Critical, 500.0, true)]
    public void Distance_limit_depends_on_urgency(Urgency urgency, double distanceKm, bool isCandidate)
    {
        var result = RunJob(Job(urgency: urgency), Team(id: 1, distanceKm: distanceKm));

        if (isCandidate)
        {
            Assert.Single(result.Candidates);
        }
        else
        {
            AssertEliminatedWith(result, 1, EliminationReason.TooFar);
        }
    }

    [Fact]
    public void Distance_limit_comes_from_settings()
    {
        var settings = TestSettings.Standard();
        settings.CityWidthKm = 10;

        var result = TeamEliminator.Eliminate(
            Job(urgency: Urgency.Low),
            new[] { Team(id: 1, distanceKm: 3) },
            Now,
            settings);

        AssertEliminatedWith(result, 1, EliminationReason.TooFar);
    }


    [Fact]
    public void Team_that_rejected_this_job_is_eliminated_with_RejectedThisJob()
    {
        var result = RunJob(
            Job(rejectedTeamIds: new HashSet<int> { 1 }),
            Team(id: 1));

        AssertEliminatedWith(result, 1, EliminationReason.RejectedThisJob);
    }

    [Fact]
    public void Rejection_is_per_job_other_teams_stay_candidates()
    {
        var result = RunJob(
            Job(rejectedTeamIds: new HashSet<int> { 1 }),
            Team(id: 1),
            Team(id: 2));

        Assert.Equal(new[] { 2 }, result.Candidates.Select(t => t.TeamId));
        Assert.Equal(new[] { 1 }, result.Eliminated.Select(e => e.TeamId));
    }


    [Fact]
    public void Critical_job_removes_only_the_distance_rule()
    {
        var result = RunJob(
            Job(urgency: Urgency.Critical),
            Team(id: 1, distanceKm: 18),
            Team(id: 2, distanceKm: 1, skillIds: new[] { 2 }),
            Team(id: 3, distanceKm: 1, locationUpdatedAt: Now.AddMinutes(-10)),
            Team(id: 4, distanceKm: 1, cooldownUntil: Now.AddMinutes(3)));

        Assert.Equal(new[] { 1 }, result.Candidates.Select(t => t.TeamId));
        Assert.Equal(new[] { 2, 3, 4 }, result.Eliminated.Select(e => e.TeamId));
        Assert.Equal(
            new[] { EliminationReason.MissingSkill, EliminationReason.StaleLocation, EliminationReason.CoolingDown },
            result.Eliminated.Select(e => e.Reason));
    }

    [Fact]
    public void Critical_job_does_not_relax_the_rejection_rule()
    {
        var result = RunJob(
            Job(urgency: Urgency.Critical, rejectedTeamIds: new HashSet<int> { 1 }),
            Team(id: 1, distanceKm: 1));

        AssertEliminatedWith(result, 1, EliminationReason.RejectedThisJob);
    }


    [Fact]
    public void Stale_location_rule_comes_before_distance_rule()
    {
        var result = RunJob(
            Job(urgency: Urgency.Low),
            Team(id: 1, distanceKm: 9, locationUpdatedAt: Now.AddMinutes(-10)));

        AssertEliminatedWith(result, 1, EliminationReason.StaleLocation);
    }

    [Fact]
    public void Distance_rule_comes_before_rejection_rule()
    {
        var result = RunJob(
            Job(urgency: Urgency.Low, rejectedTeamIds: new HashSet<int> { 1 }),
            Team(id: 1, distanceKm: 9));

        AssertEliminatedWith(result, 1, EliminationReason.TooFar);
    }

    [Fact]
    public void Rejection_rule_comes_before_cooldown_rule()
    {
        var result = RunJob(
            Job(rejectedTeamIds: new HashSet<int> { 1 }),
            Team(id: 1, cooldownUntil: Now.AddMinutes(3)));

        AssertEliminatedWith(result, 1, EliminationReason.RejectedThisJob);
    }


    [Fact]
    public void Four_team_example_for_low_urgency_street_light()
    {
        var result = RunJob(
            Job(urgency: Urgency.Low),
            Team(id: 1, distanceKm: 1),
            Team(id: 2, skillIds: new[] { 2 }),
            Team(id: 3, status: TeamStatus.OnRoute),
            Team(id: 4, distanceKm: 6));

        Assert.Equal(new[] { 1 }, result.Candidates.Select(t => t.TeamId));
        Assert.Equal(new[] { 2, 3, 4 }, result.Eliminated.Select(e => e.TeamId));
        Assert.Equal(
            new[] { EliminationReason.MissingSkill, EliminationReason.NotIdle, EliminationReason.TooFar },
            result.Eliminated.Select(e => e.Reason));
    }

    [Fact]
    public void Same_four_teams_for_critical_job_keep_the_far_team()
    {
        var result = RunJob(
            Job(urgency: Urgency.Critical),
            Team(id: 1, distanceKm: 1),
            Team(id: 2, skillIds: new[] { 2 }),
            Team(id: 3, status: TeamStatus.OnRoute),
            Team(id: 4, distanceKm: 6));

        Assert.Equal(new[] { 1, 4 }, result.Candidates.Select(t => t.TeamId));
        Assert.Equal(new[] { 2, 3 }, result.Eliminated.Select(e => e.TeamId));
    }

    [Fact]
    public void Same_input_gives_same_result()
    {
        var teams = new[]
        {
            Team(id: 1),
            Team(id: 2, distanceKm: 6),
            Team(id: 3, status: TeamStatus.Offline)
        };

        var first = RunJob(Job(), teams);
        var second = RunJob(Job(), teams);

        Assert.Equal(first.Candidates.Select(t => t.TeamId), second.Candidates.Select(t => t.TeamId));
        Assert.Equal(first.Eliminated, second.Eliminated);
    }
}