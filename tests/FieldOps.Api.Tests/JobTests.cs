using FieldOps.Api.Domain;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests;

public class JobTests
{
    [Fact]
    public void NewJob_StartsQueued_AndCopiesUrgencyFromFaultType()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var faultType = new FaultType
        {
            Id = 1,
            Name = "Gas leak",
            RequiredSkillId = 1,
            EstimatedDurationMinutes = 90,
            DefaultUrgency = Urgency.Critical
        };
        var location = new Point(32.8597, 39.9334) { SRID = 4326 };

        var job = new Job(faultType, location, "Smell of gas", "TEST-CODE", clock.UtcNow);

        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(Urgency.Critical, job.InitialUrgency);
        Assert.Equal(Urgency.Critical, job.Urgency);
        Assert.Equal(clock.UtcNow, job.CreatedAt);
    }

    [Fact]
    public void NewJob_WithNullFaultType_Throws()
    {
        var location = new Point(32.8597, 39.9334) { SRID = 4326 };

        Assert.Throws<ArgumentNullException>(() =>
            new Job(null!, location, "Smell of gas", "TEST-CODE", DateTimeOffset.UnixEpoch));
    }
}