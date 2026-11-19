using FieldOps.Api.Assignment;
using FieldOps.Api.Domain;

namespace FieldOps.Api.Tests.Assignment;

internal static class EliminationTestData
{
    // Tüm eleme testlerinin "şimdisi".
    public static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    public const int ElectricalSkillId = 3;

    public static TeamSnapshot Team(
        int id = 1,
        TeamStatus status = TeamStatus.Idle,
        double distanceKm = 1,
        DateTimeOffset? locationUpdatedAt = null,
        DateTimeOffset? cooldownUntil = null,
        int[]? skillIds = null,
        ShiftWindow[]? shifts = null)
        => new(
            id,
            status,
            distanceKm,
            locationUpdatedAt ?? Now,
            cooldownUntil,
            skillIds ?? new[] { ElectricalSkillId },
            shifts ?? new[] { new ShiftWindow(Now.AddHours(-1), Now.AddHours(7)) });

    public static JobRequest Job(
        long id = 100,
        int requiredSkillId = ElectricalSkillId,
        Urgency urgency = Urgency.Low,
        IReadOnlySet<int>? rejectedTeamIds = null)
        => new(id, requiredSkillId, urgency, rejectedTeamIds ?? new HashSet<int>());
}