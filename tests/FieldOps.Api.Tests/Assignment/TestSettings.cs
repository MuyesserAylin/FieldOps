using FieldOps.Api.Assignment;

namespace FieldOps.Api.Tests.Assignment;

internal static class TestSettings
{
    public static AssignmentSettings Standard() => new()
    {
        LocationMaxAgeSeconds = 60,
        CityWidthKm = 20,
        HighDistanceLimitPercent = 60,
        MediumDistanceLimitPercent = 40,
        LowDistanceLimitPercent = 20
    };
}