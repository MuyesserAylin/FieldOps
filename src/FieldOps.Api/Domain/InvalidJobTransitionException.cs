using System;

namespace FieldOps.Api.Domain;

public sealed class InvalidJobTransitionException : InvalidOperationException
{
    public JobStatus From { get; }
    public JobStatus To { get; }

    public InvalidJobTransitionException(JobStatus from, JobStatus to)
    : base($"Job status cannot change from {from} to {to}.")
    {
        From = from;
        To = to;
    }
}
