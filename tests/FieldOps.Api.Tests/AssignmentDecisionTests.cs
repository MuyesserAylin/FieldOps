using FieldOps.Api.Assignment;
using FieldOps.Api.Domain;

namespace FieldOps.Api.Tests;

public class AssignmentDecisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly ScoreBreakdown SampleScore = new(75, 100, 66.67, 90, 82.5);

    private static AssignmentDecision NewDecision() => new(17, Urgency.Low, WeightSet.Normal, Now);

    [Fact]
    public void NewDecision_KeepsHeaderFieldsAndHasNoCandidates()
    {
        var decision = NewDecision();

        Assert.Equal(17, decision.JobId);
        Assert.Equal(Urgency.Low, decision.Urgency);
        Assert.Equal(WeightSet.Normal, decision.WeightSet);
        Assert.Equal(Now, decision.DecidedAt);
        Assert.Empty(decision.Candidates);
        Assert.Null(decision.SelectedCandidate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_NonPositiveJobId_Throws(long jobId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AssignmentDecision(jobId, Urgency.Low, WeightSet.Normal, Now));
    }

    [Fact]
    public void Constructor_UndefinedUrgencyOrWeightSet_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AssignmentDecision(17, (Urgency)99, WeightSet.Normal, Now));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AssignmentDecision(17, Urgency.Low, (WeightSet)99, Now));
    }

    [Fact]
    public void AddEliminated_StoresReasonAndLeavesScoresEmpty()
    {
        var decision = NewDecision();

        decision.AddEliminated(9, 6.2, EliminationReason.TooFar);

        var row = Assert.Single(decision.Candidates);
        Assert.Equal(9, row.TeamId);
        Assert.Equal(6.2, row.DistanceKm);
        Assert.True(row.IsEliminated);
        Assert.Equal(EliminationReason.TooFar, row.EliminationReason);
        Assert.Null(row.DistanceScore);
        Assert.Null(row.WorkloadScore);
        Assert.Null(row.SkillScore);
        Assert.Null(row.ResponseScore);
        Assert.Null(row.TotalScore);
        Assert.Null(row.Rank);
    }

    [Fact]
    public void AddCandidate_StoresScoresAndRankAndHasNoReason()
    {
        var decision = NewDecision();

        decision.AddCandidate(3, 1.0, SampleScore, 1);

        var row = Assert.Single(decision.Candidates);
        Assert.Equal(3, row.TeamId);
        Assert.Equal(1.0, row.DistanceKm);
        Assert.False(row.IsEliminated);
        Assert.Null(row.EliminationReason);
        Assert.Equal(75.0, row.DistanceScore);
        Assert.Equal(100.0, row.WorkloadScore);
        Assert.Equal(66.67, row.SkillScore);
        Assert.Equal(90.0, row.ResponseScore);
        Assert.Equal(82.5, row.TotalScore);
        Assert.Equal(1, row.Rank);
    }

    [Fact]
    public void SelectedCandidate_IsTheRankOneTeam_WhateverTheAddOrder()
    {
        var decision = NewDecision();
        decision.AddCandidate(5, 3.0, SampleScore, 2);
        decision.AddEliminated(9, 6.2, EliminationReason.TooFar);
        decision.AddCandidate(3, 1.0, SampleScore, 1);

        Assert.Equal(3, decision.SelectedCandidate!.TeamId);
    }

    [Fact]
    public void SelectedCandidate_IsNull_WhenEveryoneIsEliminated()
    {
        var decision = NewDecision();
        decision.AddEliminated(9, 6.2, EliminationReason.TooFar);
        decision.AddEliminated(12, 2.0, EliminationReason.MissingSkill);

        Assert.Null(decision.SelectedCandidate);
    }

    [Fact]
    public void SameTeamTwice_Throws_EvenIfFirstWasEliminatedAndSecondIsCandidate()
    {
        var decision = NewDecision();
        decision.AddEliminated(9, 6.2, EliminationReason.TooFar);

        Assert.Throws<InvalidOperationException>(() => decision.AddCandidate(9, 6.2, SampleScore, 1));
        Assert.Throws<InvalidOperationException>(() => decision.AddEliminated(9, 6.2, EliminationReason.NotIdle));
        Assert.Single(decision.Candidates);
    }

    [Fact]
    public void SameRankTwice_Throws()
    {
        var decision = NewDecision();
        decision.AddCandidate(3, 1.0, SampleScore, 1);

        Assert.Throws<InvalidOperationException>(() => decision.AddCandidate(5, 3.0, SampleScore, 1));
        Assert.Single(decision.Candidates);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddCandidate_RankBelowOne_Throws(int rank)
    {
        var decision = NewDecision();

        Assert.Throws<ArgumentOutOfRangeException>(() => decision.AddCandidate(3, 1.0, SampleScore, rank));
        Assert.Empty(decision.Candidates);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidDistance_Throws(double distanceKm)
    {
        var decision = NewDecision();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => decision.AddEliminated(9, distanceKm, EliminationReason.TooFar));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => decision.AddCandidate(3, distanceKm, SampleScore, 1));
        Assert.Empty(decision.Candidates);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void NonPositiveTeamId_Throws(int teamId)
    {
        var decision = NewDecision();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => decision.AddEliminated(teamId, 1.0, EliminationReason.TooFar));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => decision.AddCandidate(teamId, 1.0, SampleScore, 1));
    }

    [Fact]
    public void UndefinedEliminationReason_Throws()
    {
        var decision = NewDecision();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => decision.AddEliminated(9, 1.0, (EliminationReason)99));
    }

    [Fact]
    public void NullScore_Throws()
    {
        var decision = NewDecision();

        Assert.Throws<ArgumentNullException>(() => decision.AddCandidate(3, 1.0, null!, 1));
    }
}