using System;

namespace FieldOps.Api.Domain;

public static class JobStatusTransitions
{
    private static readonly HashSet<(JobStatus From, JobStatus To)> Allowed =
    [
        (JobStatus.Queued,JobStatus.Offered),
        (JobStatus.Queued,JobStatus.Cancelled),
        (JobStatus.Offered,JobStatus.OnRoute),
        (JobStatus.Offered,JobStatus.Queued),
        (JobStatus.Offered,JobStatus.Cancelled),
        (JobStatus.OnRoute,JobStatus.OnSite),
        (JobStatus.OnRoute,JobStatus.Queued),
        (JobStatus.OnRoute,JobStatus.Cancelled),
        (JobStatus.OnSite,JobStatus.Completed),
        (JobStatus.OnSite,JobStatus.Queued),
        (JobStatus.OnSite,JobStatus.Cancelled),

];
    public static bool IsAllowed(JobStatus from, JobStatus to)
    => Allowed.Contains((from, to));
}
