using System;
using FieldOps.Api.Domain;

namespace FieldOps.Api.Jobs;

public static class OperatorStatusPolicy
{
    // Ay 1: operatör yalnızca işi iptal edebilir. Diğer geçişleri atama motoru (Queued -> Offered)
    // ve ekip (Offered -> OnRoute -> OnSite -> Completed) yapar. Elle ekip seçme ayrı bir
    // endpoint olacak (Ay 3), çünkü ekip numarası gerektirir.
    private static readonly JobStatus[] Allowed = [JobStatus.Cancelled];

    public static string Message =>
        $"Operators can only set status to: {string.Join(", ", Allowed)}.";

    public static bool CanSet(JobStatus status) => Allowed.Contains(status);
}
