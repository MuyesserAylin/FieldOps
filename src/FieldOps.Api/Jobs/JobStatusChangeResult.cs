using System;
using FieldOps.Api.Domain;

namespace FieldOps.Api.Jobs;

public enum JobStatusChangeOutcome
{
    Changed,
    NotFound,
    InvalidTransition,
    ConcurrencyConflict
}
public sealed record JobStatusChangeResult(
   JobStatusChangeOutcome Outcome,
   JobStatus? OldStatus = null,
   JobStatus? NewStatus = null)
{
    public static JobStatusChangeResult Changed(JobStatus oldStatus, JobStatus newStatus)
    => new(JobStatusChangeOutcome.Changed, oldStatus, newStatus);

    public static JobStatusChangeResult NotFound()
    => new(JobStatusChangeOutcome.NotFound);

    public static JobStatusChangeResult InvalidTransition()
      => new(JobStatusChangeOutcome.InvalidTransition);

    public static JobStatusChangeResult ConcurrencyConflict()
    => new(JobStatusChangeOutcome.ConcurrencyConflict);
}
