using FieldOps.Api.Domain;

namespace FieldOps.Api.Assignment;

// Toplam puan ve sıralama. Saf fonksiyonlar: veritabanına, saate, rastgeleliğe bakmaz.
public static class TeamScorer
{
    // Tek bir ekibin dört notunu ve toplam puanını hesaplar.
    // Toplam = (w1 x Mesafe + w2 x İşYükü + w3 x Beceri + w4 x GeçmişCevap) / 100
    // Ağırlık seti işin aciliyetine göre seçilir: kritik/yüksek -> acil set, orta/düşük -> normal set.
    // Dört not ayrı ayrı döndürülür: "bu ekip neden bu puanı aldı?" sorusu karar kaydında cevaplanacak.
    public static ScoreBreakdown Score(ScoringTeam team, Urgency urgency, AssignmentSettings settings)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(settings);

        var weights = settings.GetWeights(urgency);
        var referenceKm = settings.GetDistanceReferenceKm(urgency);

        var distance = CriterionScores.Distance(team.DistanceKm, referenceKm);
        var workload = CriterionScores.Workload(team.WorkedMinutes, team.ShiftMinutes, settings.WorkloadCapacityRatio);
        var skill = CriterionScores.Skill(team.SkillLevel);
        var response = CriterionScores.Response(team.OffersConsidered, team.OffersAccepted, settings.ResponseMinOffers);

        var total = (weights.Distance * distance
                     + weights.Workload * workload
                     + weights.Skill * skill
                     + weights.Response * response) / 100;

        return new ScoreBreakdown(distance, workload, skill, response, total);
    }



    // Ekipleri puanlayıp en iyiden en kötüye sıralar. İlk sıradaki ekibe teklif gider.
    // Eşitlik kuralı : puan eşitse önce daha yakın olan, sonra daha az çalışmış olan,
    // sonra küçük numaralı ekip. Rastgelelik yoktur: aynı girdi her zaman aynı sırayı verir.
    // Girdi koleksiyonu değiştirilmez. Aynı ekip iki kez gelirse hata verilir (bir yerde hata var demektir).
    public static IReadOnlyList<ScoredTeam> Rank(IEnumerable<ScoringTeam> teams, Urgency urgency, AssignmentSettings settings)
    {
        ArgumentNullException.ThrowIfNull(teams);
        ArgumentNullException.ThrowIfNull(settings);

        var items = new List<(ScoringTeam Team, ScoreBreakdown Breakdown)>();
        var seenTeamIds = new HashSet<int>();

        foreach (var team in teams)
        {
            if (team is null)
            {
                throw new ArgumentException("Ekip listesinde boş (null) eleman olamaz.", nameof(teams));
            }

            if (!seenTeamIds.Add(team.TeamId))
            {
                throw new ArgumentException($"Ekip {team.TeamId} listede birden fazla kez var.", nameof(teams));
            }

            items.Add((team, Score(team, urgency, settings)));
        }

        return items
            .OrderByDescending(item => Math.Round(item.Breakdown.Total, 6))
            .ThenBy(item => item.Team.DistanceKm)
            .ThenBy(item => item.Team.WorkedMinutes)
            .ThenBy(item => item.Team.TeamId)
            .Select(item => new ScoredTeam(item.Team.TeamId, item.Breakdown))
            .ToList();
    }
}