using FieldOps.Api.Assignment;
using FieldOps.Api.Domain;

namespace FieldOps.Api.Tests.Assignment;

public class CriterionScoresTests
{

    [Theory]
    [InlineData(4, 10, 60)]
    [InlineData(2, 10, 80)]
    [InlineData(6, 10, 40)]
    [InlineData(1, 4, 75)]
    [InlineData(3, 4, 25)]
    [InlineData(5, 25, 80)]
    [InlineData(0, 10, 100)]
    [InlineData(10, 10, 0)]
    [InlineData(15, 10, 0)]
    public void Distance_gives_expected_score(double distanceKm, double referenceKm, double expected)
    {
        Assert.Equal(expected, CriterionScores.Distance(distanceKm, referenceKm), 2);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(5, 0)]
    [InlineData(5, -4)]
    [InlineData(double.NaN, 10)]
    [InlineData(5, double.NaN)]
    [InlineData(double.PositiveInfinity, 10)]
    [InlineData(5, double.PositiveInfinity)]
    public void Distance_rejects_invalid_input(double distanceKm, double referenceKm)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CriterionScores.Distance(distanceKm, referenceKm));
    }


    [Theory]
    [InlineData(0, 100)]
    [InlineData(30, 91.67)]
    [InlineData(60, 83.33)]
    [InlineData(90, 75)]
    [InlineData(180, 50)]
    [InlineData(300, 16.67)]
    [InlineData(360, 0)]
    [InlineData(450, 0)]
    public void Workload_gives_expected_score_for_8_hour_shift(double workedMinutes, double expected)
    {
        Assert.Equal(expected, CriterionScores.Workload(workedMinutes, 480, 0.75), 2);
    }

    [Fact]
    public void Workload_uses_the_shift_length_for_capacity()
    {
        Assert.Equal(50.0, CriterionScores.Workload(270, 720, 0.75), 2);
    }

    [Theory]
    [InlineData(-1, 480, 0.75)]
    [InlineData(100, 0, 0.75)]
    [InlineData(100, -480, 0.75)]
    [InlineData(100, 480, 0)]
    [InlineData(100, 480, 1.1)]
    [InlineData(double.NaN, 480, 0.75)]
    [InlineData(100, double.NaN, 0.75)]
    [InlineData(100, 480, double.NaN)]
    public void Workload_rejects_invalid_input(double workedMinutes, double shiftMinutes, double capacityRatio)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CriterionScores.Workload(workedMinutes, shiftMinutes, capacityRatio));
    }


    [Theory]
    [InlineData(TeamSkillLevel.Expert, 100)]
    [InlineData(TeamSkillLevel.Intermediate, 66.67)]
    [InlineData(TeamSkillLevel.Basic, 33.33)]
    public void Skill_gives_expected_score(TeamSkillLevel level, double expected)
    {
        Assert.Equal(expected, CriterionScores.Skill(level), 2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void Skill_rejects_undefined_level(int rawLevel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CriterionScores.Skill((TeamSkillLevel)rawLevel));
    }


    [Theory]
    [InlineData(10, 7, 5, 70)]
    [InlineData(10, 6, 5, 60)]
    [InlineData(10, 9, 5, 90)]
    [InlineData(10, 8, 5, 80)]
    [InlineData(4, 0, 5, 100)]
    [InlineData(0, 0, 5, 100)]
    [InlineData(5, 0, 5, 0)]
    [InlineData(5, 5, 5, 100)]
    [InlineData(0, 0, 0, 100)]
    public void Response_gives_expected_score(int considered, int accepted, int minOffers, double expected)
    {
        Assert.Equal(expected, CriterionScores.Response(considered, accepted, minOffers), 2);
    }

    [Theory]
    [InlineData(-1, 0, 5)]
    [InlineData(5, -1, 5)]
    [InlineData(5, 6, 5)]
    [InlineData(4, 5, 5)]
    [InlineData(5, 3, -1)]
    public void Response_rejects_invalid_input(int considered, int accepted, int minOffers)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CriterionScores.Response(considered, accepted, minOffers));
    }


    [Fact]
    public void All_scores_stay_between_0_and_100_for_extreme_values()
    {
        double[] distances = { 0, 0.001, 4, 1000 };
        double[] workedMinutes = { 0, 0.001, 360, 100000 };

        foreach (var d in distances)
        {
            AssertInRange(CriterionScores.Distance(d, 4));
        }

        foreach (var w in workedMinutes)
        {
            AssertInRange(CriterionScores.Workload(w, 480, 0.75));
        }

        foreach (var level in Enum.GetValues<TeamSkillLevel>())
        {
            AssertInRange(CriterionScores.Skill(level));
        }

        for (var considered = 0; considered <= 12; considered++)
        {
            for (var accepted = 0; accepted <= considered; accepted++)
            {
                AssertInRange(CriterionScores.Response(considered, accepted, 5));
            }
        }
    }

    private static void AssertInRange(double score)
    {
        Assert.InRange(score, 0, 100);
    }
}