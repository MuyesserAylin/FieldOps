using FieldOps.Api.Domain;

namespace FieldOps.Api.Assignment;

// Ekip eleme: bir iş için hangi ekipler aday olabilir, hangileri neden elenir.
// Saf fonksiyon: veritabanına bakmaz, saati dışarıdan alır, rastgelelik yoktur, hiçbir şeyi değiştirmez.
public static class TeamEliminator
{
    public static EliminationResult Eliminate(
        JobRequest job,
        IReadOnlyList<TeamSnapshot> teams,
        DateTimeOffset now,
        AssignmentSettings settings)
    {
        var candidates = new List<TeamSnapshot>();
        var eliminated = new List<EliminatedTeam>();

        foreach (var team in teams)
        {
            var reason = FirstFailingRule(job, team, now, settings);

            if (reason is null)
            {
                candidates.Add(team);
            }
            else
            {
                eliminated.Add(new EliminatedTeam(team.TeamId, reason.Value));
            }
        }

        return new EliminationResult(candidates, eliminated);
    }


    private static EliminationReason? FirstFailingRule(
        JobRequest job,
        TeamSnapshot team,
        DateTimeOffset now,
        AssignmentSettings settings)
    {
        //1.Beceri
        if (!team.SkillIds.Contains(job.RequiredSkillId))
        {
            return EliminationReason.MissingSkill;
        }

        //2.Vardiya
        if (!IsOnShift(team, now))
        {
            return EliminationReason.NotOnShift;
        }

        //3.Boşta mı (Çift atamanın ilk savunması)
        if (team.Status != TeamStatus.Idle)
        {
            return EliminationReason.NotIdle;
        }

        // 4) Konum bilgisi çok eski mi
        if (IsLocationStale(team, now, settings))
        {
            return EliminationReason.StaleLocation;
        }

        // 5) Mesafe sınırı (aciliyete göre; kritikte sınır yok)
        if (IsTooFar(job, team, settings))
        {
            return EliminationReason.TooFar;
        }

        // 6) Bu işi daha önce reddetmiş veya yarıda bırakmış mı
        if (job.RejectedTeamIds.Contains(team.TeamId))
        {
            return EliminationReason.RejectedThisJob;
        }


        // 7) Yakın zamanda cevapsız bırakmış, bekletmede mi
        if (IsCoolingDown(team, now))
        {
            return EliminationReason.CoolingDown;
        }

        return null;
    }


    private static bool IsOnShift(TeamSnapshot team, DateTimeOffset now)
        => team.Shifts.Any(s => s.StartsAt <= now && now < s.EndsAt);

    private static bool IsLocationStale(TeamSnapshot team, DateTimeOffset now, AssignmentSettings settings)
        => (now - team.LocationUpdatedAt).TotalSeconds > settings.LocationMaxAgeSeconds;

    private static bool IsTooFar(JobRequest job, TeamSnapshot team, AssignmentSettings settings)
        => settings.GetDistanceLimitKm(job.Urgency) is { } limitKm && team.DistanceKm > limitKm;

    private static bool IsCoolingDown(TeamSnapshot team, DateTimeOffset now)
        => team.CooldownUntil is { } until && now < until;
}