using FieldOps.Api.Domain;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests;

internal static class JobTestHelper
{
    private static readonly DateTimeOffset SetupTime =
        new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static Job CreateQueuedJob()
    {
        var faultType = new FaultType
        {
            Id = 1,
            Name = "Gas leak",
            RequiredSkillId = 1,
            EstimatedDurationMinutes = 90,
            DefaultUrgency = Urgency.Critical
        };
        var location = new Point(32.8597, 39.9334) { SRID = 4326 };

        return new Job(faultType, location, "Smell of gas", "TEST-CODE", SetupTime);
    }

    public static Job CreateJobInStatus(JobStatus status)
    {
        var job = CreateQueuedJob();

        JobStatus[] path = status switch
        {
            JobStatus.Queued => [],
            JobStatus.Offered => [JobStatus.Offered],
            JobStatus.OnRoute => [JobStatus.Offered, JobStatus.OnRoute],
            JobStatus.OnSite => [JobStatus.Offered, JobStatus.OnRoute, JobStatus.OnSite],
            JobStatus.Completed => [JobStatus.Offered, JobStatus.OnRoute, JobStatus.OnSite, JobStatus.Completed],
            JobStatus.Cancelled => [JobStatus.Cancelled],
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        foreach (var step in path)
        {
            job.TransitionTo(step, SetupTime);
        }

        return job;
    }
}