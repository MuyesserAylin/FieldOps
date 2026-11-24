using FieldOps.Api.Domain;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests;

public class JobEscalationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Job CreateQueuedJob(Urgency urgency)
    {
        var faultType = new FaultType
        {
            Id = 1,
            Name = "Street lamp",
            RequiredSkillId = 3,
            EstimatedDurationMinutes = 30,
            DefaultUrgency = urgency
        };
        var location = new Point(32.8597, 39.9334) { SRID = 4326 };

        return new Job(faultType, location, "Lamp is out", "TEST-ESC", Now);
    }

    [Fact]
    public void LowToMedium_ReturnsTrue_ChangesUrgency_AndKeepsInitialUrgency()
    {
        var job = CreateQueuedJob(Urgency.Low);

        var changed = job.EscalateUrgency(Urgency.Medium);

        Assert.True(changed);
        Assert.Equal(Urgency.Medium, job.Urgency);
        Assert.Equal(Urgency.Low, job.InitialUrgency);
    }

    [Fact]
    public void LowToHigh_InOneCall_IsAllowed()
    {
        var job = CreateQueuedJob(Urgency.Low);

        Assert.True(job.EscalateUrgency(Urgency.High));
        Assert.Equal(Urgency.High, job.Urgency);
        Assert.Equal(Urgency.Low, job.InitialUrgency);
    }

    [Fact]
    public void SameUrgencyAgain_ReturnsFalse_AndChangesNothing()
    {
        var job = CreateQueuedJob(Urgency.Low);
        job.EscalateUrgency(Urgency.Medium);

        Assert.False(job.EscalateUrgency(Urgency.Medium));
        Assert.Equal(Urgency.Medium, job.Urgency);
    }

    [Fact]
    public void LoweringUrgency_ReturnsFalse_AndUrgencyStays()
    {
        var job = CreateQueuedJob(Urgency.Medium);

        Assert.False(job.EscalateUrgency(Urgency.Low));
        Assert.Equal(Urgency.Medium, job.Urgency);
    }

    [Fact]
    public void CriticalJob_IsNotChanged_ByEscalationToHigh()
    {
        var job = CreateQueuedJob(Urgency.Critical);

        Assert.False(job.EscalateUrgency(Urgency.High));
        Assert.Equal(Urgency.Critical, job.Urgency);
    }

    [Fact]
    public void EscalatingToCritical_Throws_AndUrgencyStays()
    {
        var job = CreateQueuedJob(Urgency.Low);

        Assert.Throws<ArgumentOutOfRangeException>(() => job.EscalateUrgency(Urgency.Critical));
        Assert.Equal(Urgency.Low, job.Urgency);
    }

    [Fact]
    public void UndefinedUrgency_Throws()
    {
        var job = CreateQueuedJob(Urgency.Low);

        Assert.Throws<ArgumentOutOfRangeException>(() => job.EscalateUrgency((Urgency)99));
    }

    [Theory]
    [InlineData(JobStatus.Offered)]
    [InlineData(JobStatus.OnRoute)]
    [InlineData(JobStatus.OnSite)]
    [InlineData(JobStatus.Completed)]
    [InlineData(JobStatus.Cancelled)]
    public void JobNotInQueue_Throws_AndUrgencyStays(JobStatus status)
    {
        var job = JobTestHelper.CreateJobInStatus(status);
        var before = job.Urgency;

        Assert.Throws<InvalidOperationException>(() => job.EscalateUrgency(Urgency.High));
        Assert.Equal(before, job.Urgency);
    }
}