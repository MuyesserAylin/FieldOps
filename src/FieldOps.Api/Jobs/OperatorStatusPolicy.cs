using System;
using FieldOps.Api.Domain;

namespace FieldOps.Api.Jobs;

public static class OperatorStatusPolicy
{
    private static readonly JobStatus[] Allowed = [JobStatus.Cancelled];

    public static string Message =>
        $"Operators can only set status to: {string.Join(", ", Allowed)}.";

    public static bool CanSet(JobStatus status) => Allowed.Contains(status);
}
