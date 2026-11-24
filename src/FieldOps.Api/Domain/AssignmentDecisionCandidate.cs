using FieldOps.Api.Assignment;

namespace FieldOps.Api.Domain;

// Karar günlüğünün bir satırı: bir kararda değerlendirilen bir ekip.
// İki hâlden biridir:
//  - Elenen ekip: EliminationReason dolu; notlar, puan ve sıra boş.
//  - Aday ekip: notlar, toplam puan ve sıra dolu; EliminationReason boş.
public class AssignmentDecisionCandidate
{
    private AssignmentDecisionCandidate() { }

    // Elenen ekip
    internal AssignmentDecisionCandidate(int teamId, double distanceKm, EliminationReason reason)
    {
        TeamId = teamId;
        DistanceKm = distanceKm;
        EliminationReason = reason;
    }

    // Aday ekip
    internal AssignmentDecisionCandidate(int teamId, double distanceKm, ScoreBreakdown score, int rank)
    {
        TeamId = teamId;
        DistanceKm = distanceKm;
        DistanceScore = score.Distance;
        WorkloadScore = score.Workload;
        SkillScore = score.Skill;
        ResponseScore = score.Response;
        TotalScore = score.Total;
        Rank = rank;
    }

    public long Id { get; private set; }
    public long DecisionId { get; private set; }
    public AssignmentDecision Decision { get; private set; } = null!;

    public int TeamId { get; private set; }
    public Team Team { get; private set; } = null!;

    public double DistanceKm { get; private set; }
    public EliminationReason? EliminationReason { get; private set; }
    public double? DistanceScore { get; private set; }
    public double? WorkloadScore { get; private set; }
    public double? SkillScore { get; private set; }
    public double? ResponseScore { get; private set; }
    public double? TotalScore { get; private set; }
    public int? Rank { get; private set; }
    public bool IsEliminated => EliminationReason is not null;
}