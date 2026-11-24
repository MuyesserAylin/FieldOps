using FieldOps.Api.Assignment;

namespace FieldOps.Api.Tests.Assignment;

internal static class TestSettings
{
    public static AssignmentSettings Standard() => new()
    {
        LocationMaxAgeSeconds = 60,
        CityWidthKm = 20,
        CityHeightKm = 15,
        HighDistanceLimitPercent = 60,
        MediumDistanceLimitPercent = 40,
        LowDistanceLimitPercent = 20,
        WorkloadCapacityRatio = 0.75,
        ResponseHistoryWindow = 10,
        ResponseMinOffers = 5,
        UrgencyEscalationMinutes = 30,
        UrgentWeights = new ScoringWeights { Distance = 60, Workload = 5, Skill = 10, Response = 25 },
        NormalWeights = new ScoringWeights { Distance = 35, Workload = 40, Skill = 15, Response = 10 }
    };
}