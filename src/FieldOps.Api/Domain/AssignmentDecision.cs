using FieldOps.Api.Assignment;

namespace FieldOps.Api.Domain;

// Atama karar günlüğünün üst kaydı (market fişinin üst kısmı):
// hangi iş için, hangi aciliyetle, hangi ağırlık setiyle, ne zaman karar verildi.
// Satırları (her ekip için bir kayıt) Candidates listesindedir.
// "Önerilen ekip" için ayrı alan yoktur: sırası 1 olan aday önerilen ekiptir (aynı bilgiyi iki yerde tutmamak için).
// Aciliyet karara yazılır: bekleyen işin aciliyeti sonradan yükselse bile
public class AssignmentDecision
{
    private readonly List<AssignmentDecisionCandidate> _candidates = new();

    private AssignmentDecision() { }

    public AssignmentDecision(long jobId, Urgency urgency, WeightSet weightSet, DateTimeOffset decidedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(jobId);

        if (!Enum.IsDefined(urgency))
        {
            throw new ArgumentOutOfRangeException(nameof(urgency), urgency, "Bilinmeyen aciliyet.");
        }

        if (!Enum.IsDefined(weightSet))
        {
            throw new ArgumentOutOfRangeException(nameof(weightSet), weightSet, "Bilinmeyen ağırlık seti.");
        }

        JobId = jobId;
        Urgency = urgency;
        WeightSet = weightSet;
        DecidedAt = decidedAt;
    }

    public void AddEliminated(int teamId, double distanceKm, EliminationReason reason)
    {
        ValidateNewTeam(teamId, distanceKm);

        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Bilinmeyen elenme nedeni.");
        }

        _candidates.Add(new AssignmentDecisionCandidate(teamId, distanceKm, reason));
    }

    public void AddCandidate(int teamId, double distanceKm, ScoreBreakdown score, int rank)
    {
        ArgumentNullException.ThrowIfNull(score);
        ArgumentOutOfRangeException.ThrowIfLessThan(rank, 1);
        ValidateNewTeam(teamId, distanceKm);

        if (_candidates.Any(c => c.Rank == rank))
        {
            throw new InvalidOperationException($"Bu kararda {rank}. sıra zaten başka bir adaya verilmiş.");
        }

        _candidates.Add(new AssignmentDecisionCandidate(teamId, distanceKm, score, rank));
    }

    private void ValidateNewTeam(int teamId, double distanceKm)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(teamId);

        if (!double.IsFinite(distanceKm) || distanceKm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceKm), distanceKm, "Mesafe 0 veya daha büyük, sonlu bir sayı olmalı.");
        }

        if (_candidates.Any(c => c.TeamId == teamId))
        {
            throw new InvalidOperationException($"Ekip {teamId} bu kararda zaten var.");
        }
    }

    public long Id { get; private set; }
    public long JobId { get; private set; }
    public Job Job { get; private set; } = null!;

    public Urgency Urgency { get; private set; }
    public WeightSet WeightSet { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }

    public IReadOnlyCollection<AssignmentDecisionCandidate> Candidates => _candidates;

    public AssignmentDecisionCandidate? SelectedCandidate => _candidates.FirstOrDefault(c => c.Rank == 1);
}