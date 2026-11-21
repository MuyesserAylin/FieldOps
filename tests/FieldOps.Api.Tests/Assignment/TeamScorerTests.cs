using FieldOps.Api.Assignment;
using FieldOps.Api.Domain;

namespace FieldOps.Api.Tests.Assignment;

public class TeamScorerTests
{
    private const double ShiftMinutes = 480; // kapasite 0,75 x 480 = 360 dk

    private static ScoringTeam Team(
        int id, double distanceKm, double workedMinutes, TeamSkillLevel level, int considered, int accepted)
        => new(id, distanceKm, workedMinutes, ShiftMinutes, level, considered, accepted);

    private static AssignmentSettings SettingsWithReferences(
        double? highKm = null, double? mediumKm = null, double? lowKm = null)
    {
        var settings = TestSettings.Standard();

        if (highKm is { } high)
        {
            settings.HighDistanceLimitPercent = high / settings.CityWidthKm * 100;
        }

        if (mediumKm is { } medium)
        {
            settings.MediumDistanceLimitPercent = medium / settings.CityWidthKm * 100;
        }

        if (lowKm is { } low)
        {
            settings.LowDistanceLimitPercent = low / settings.CityWidthKm * 100;
        }

        return settings;
    }

    [Fact]
    public void Example1_single_team_urgent_job_scores_66()
    {
        var settings = SettingsWithReferences(highKm: 10);
        var team = Team(1, 4, 180, TeamSkillLevel.Expert, 10, 7);

        var score = TeamScorer.Score(team, Urgency.High, settings);

        Assert.Equal(60.0, score.Distance, 2);
        Assert.Equal(50.0, score.Workload, 2);
        Assert.Equal(100.0, score.Skill, 2);
        Assert.Equal(70.0, score.Response, 2);
        Assert.Equal(66.0, score.Total, 2);
    }

    private static readonly ScoringTeam ExampleTeamA = Team(1, 2, 300, TeamSkillLevel.Intermediate, 10, 6);
    private static readonly ScoringTeam ExampleTeamB = Team(2, 6, 60, TeamSkillLevel.Expert, 10, 9);

    [Fact]
    public void Example2_urgent_job_closer_team_wins()
    {
        var settings = SettingsWithReferences(highKm: 10, mediumKm: 10);

        var a = TeamScorer.Score(ExampleTeamA, Urgency.High, settings);
        var b = TeamScorer.Score(ExampleTeamB, Urgency.High, settings);

        Assert.Equal(80.0, a.Distance, 2);
        Assert.Equal(16.67, a.Workload, 2);
        Assert.Equal(66.67, a.Skill, 2);
        Assert.Equal(60.0, a.Response, 2);
        Assert.Equal(70.50, a.Total, 2);

        Assert.Equal(40.0, b.Distance, 2);
        Assert.Equal(83.33, b.Workload, 2);
        Assert.Equal(100.0, b.Skill, 2);
        Assert.Equal(90.0, b.Response, 2);
        Assert.Equal(60.67, b.Total, 2);

        var ranked = TeamScorer.Rank(new[] { ExampleTeamA, ExampleTeamB }, Urgency.High, settings);
        Assert.Equal(1, ranked[0].TeamId);
    }

    [Fact]
    public void Example2_normal_job_rested_team_wins()
    {
        var settings = SettingsWithReferences(highKm: 10, mediumKm: 10);

        var a = TeamScorer.Score(ExampleTeamA, Urgency.Medium, settings);
        var b = TeamScorer.Score(ExampleTeamB, Urgency.Medium, settings);

        Assert.Equal(50.67, a.Total, 2);
        Assert.Equal(71.33, b.Total, 2);

        var ranked = TeamScorer.Rank(new[] { ExampleTeamA, ExampleTeamB }, Urgency.Medium, settings);
        Assert.Equal(2, ranked[0].TeamId);
    }


    private static readonly ScoringTeam Ali = Team(1, 1, 300, TeamSkillLevel.Expert, 10, 8);
    private static readonly ScoringTeam Ayse = Team(2, 3, 30, TeamSkillLevel.Intermediate, 10, 9);

    [Fact]
    public void Example3_normal_job_rested_Ayse_wins()
    {
        var settings = TestSettings.Standard();

        var ali = TeamScorer.Score(Ali, Urgency.Low, settings);
        var ayse = TeamScorer.Score(Ayse, Urgency.Low, settings);

        Assert.Equal(55.92, ali.Total, 2);
        Assert.Equal(64.42, ayse.Total, 2);

        var ranked = TeamScorer.Rank(new[] { Ali, Ayse }, Urgency.Low, settings);
        Assert.Equal(2, ranked[0].TeamId);
    }

    [Fact]
    public void Example3_urgent_job_closer_Ali_wins()
    {
        var settings = SettingsWithReferences(highKm: 4);

        var ali = TeamScorer.Score(Ali, Urgency.High, settings);
        var ayse = TeamScorer.Score(Ayse, Urgency.High, settings);

        Assert.Equal(75.83, ali.Total, 2);
        Assert.Equal(48.75, ayse.Total, 2);

        var ranked = TeamScorer.Rank(new[] { Ali, Ayse }, Urgency.High, settings);
        Assert.Equal(1, ranked[0].TeamId);
    }


    [Fact]
    public void Critical_job_uses_diagonal_reference_and_urgent_weights()
    {
        var settings = TestSettings.Standard();
        var team = Team(1, 5, 0, TeamSkillLevel.Expert, 0, 0);

        var score = TeamScorer.Score(team, Urgency.Critical, settings);

        Assert.Equal(80.0, score.Distance, 2);
        Assert.Equal(88.0, score.Total, 2);
    }


    private static AssignmentSettings SkillOnlySettings()
    {
        var settings = TestSettings.Standard();
        settings.UrgentWeights = new ScoringWeights { Distance = 0, Workload = 0, Skill = 100, Response = 0 };
        return settings;
    }

    [Fact]
    public void Tie_is_broken_by_distance_first()
    {
        var settings = SkillOnlySettings();
        var far = Team(1, 5, 100, TeamSkillLevel.Expert, 10, 10);
        var near = Team(7, 2, 100, TeamSkillLevel.Expert, 10, 10);

        var ranked = TeamScorer.Rank(new[] { far, near }, Urgency.High, settings);

        Assert.Equal(new[] { 7, 1 }, ranked.Select(r => r.TeamId).ToArray());
    }

    [Fact]
    public void Tie_with_same_distance_is_broken_by_less_worked_minutes()
    {
        var settings = SkillOnlySettings();
        var busy = Team(2, 3, 120, TeamSkillLevel.Expert, 10, 10);
        var fresh = Team(5, 3, 60, TeamSkillLevel.Expert, 10, 10);

        var ranked = TeamScorer.Rank(new[] { busy, fresh }, Urgency.High, settings);

        Assert.Equal(new[] { 5, 2 }, ranked.Select(r => r.TeamId).ToArray());
    }

    [Fact]
    public void Full_tie_is_broken_by_smaller_team_id()
    {
        var settings = SkillOnlySettings();
        var teams = new[]
        {
            Team(3, 3, 60, TeamSkillLevel.Expert, 10, 10),
            Team(1, 3, 60, TeamSkillLevel.Expert, 10, 10),
            Team(2, 3, 60, TeamSkillLevel.Expert, 10, 10)
        };

        var ranked = TeamScorer.Rank(teams, Urgency.High, settings);

        Assert.Equal(new[] { 1, 2, 3 }, ranked.Select(r => r.TeamId).ToArray());
    }


    [Theory]
    [InlineData(Urgency.Medium, new[] { 3, 2, 1 })]
    [InlineData(Urgency.High, new[] { 3, 1, 2 })]
    public void Rank_gives_same_order_whatever_the_input_order(Urgency urgency, int[] expectedOrder)
    {
        var settings = SettingsWithReferences(highKm: 10, mediumKm: 10);
        var teamA = Team(1, 2, 300, TeamSkillLevel.Intermediate, 10, 6);
        var teamB = Team(2, 6, 60, TeamSkillLevel.Expert, 10, 9);
        var teamC = Team(3, 1, 100, TeamSkillLevel.Basic, 10, 10);

        var forward = TeamScorer.Rank(new[] { teamA, teamB, teamC }, urgency, settings);
        var reversed = TeamScorer.Rank(new[] { teamC, teamB, teamA }, urgency, settings);

        Assert.Equal(expectedOrder, forward.Select(r => r.TeamId).ToArray());
        Assert.Equal(expectedOrder, reversed.Select(r => r.TeamId).ToArray());
    }

    [Fact]
    public void Rank_does_not_change_the_input_list()
    {
        var settings = TestSettings.Standard();
        var input = new List<ScoringTeam>
        {
            Team(2, 6, 60, TeamSkillLevel.Expert, 10, 9),
            Team(1, 2, 300, TeamSkillLevel.Intermediate, 10, 6)
        };
        var before = input.ToList();

        TeamScorer.Rank(input, Urgency.High, settings);

        Assert.Equal(before, input);
    }

    [Fact]
    public void Rank_of_empty_list_is_empty()
    {
        var ranked = TeamScorer.Rank(Array.Empty<ScoringTeam>(), Urgency.High, TestSettings.Standard());

        Assert.Empty(ranked);
    }

    [Fact]
    public void Rank_rejects_same_team_id_twice()
    {
        var teams = new[]
        {
            Team(1, 2, 60, TeamSkillLevel.Expert, 10, 9),
            Team(1, 3, 90, TeamSkillLevel.Basic, 10, 5)
        };

        Assert.Throws<ArgumentException>(() => TeamScorer.Rank(teams, Urgency.High, TestSettings.Standard()));
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var settings = TestSettings.Standard();
        var team = Team(1, 2, 60, TeamSkillLevel.Expert, 10, 9);

        Assert.Throws<ArgumentNullException>(() => TeamScorer.Score(null!, Urgency.High, settings));
        Assert.Throws<ArgumentNullException>(() => TeamScorer.Score(team, Urgency.High, null!));
        Assert.Throws<ArgumentNullException>(() => TeamScorer.Rank(null!, Urgency.High, settings));
        Assert.Throws<ArgumentNullException>(() => TeamScorer.Rank(new[] { team }, Urgency.High, null!));
    }


    [Theory]
    [InlineData(Urgency.Critical)]
    [InlineData(Urgency.High)]
    [InlineData(Urgency.Medium)]
    [InlineData(Urgency.Low)]
    public void Total_stays_between_0_and_100_for_extreme_teams(Urgency urgency)
    {
        var settings = TestSettings.Standard();
        var best = Team(1, 0, 0, TeamSkillLevel.Expert, 10, 10);
        var worst = Team(2, 500, 100000, TeamSkillLevel.Basic, 10, 0);

        Assert.InRange(TeamScorer.Score(best, urgency, settings).Total, 0, 100);
        Assert.InRange(TeamScorer.Score(worst, urgency, settings).Total, 0, 100);
    }
}