using FieldOps.Api.Data;
using FieldOps.Api.Domain;
using FieldOps.Api.Time;
using Microsoft.Extensions.Options;

namespace FieldOps.Api.Assignment;

// Bir iş için ekip ÖNERİR: yükleyici -> eleme -> puanlama -> karar günlüğü (ana belge 7).
// Orkestra şefidir: kendisi hesap yapmaz, parçaları sırayla çağırır.
// İşe ekip ATAMAZ; iş, ekip ya da başka hiçbir tablo değişmez. Sadece karar günlüğüne yazar.
// Gerçek atama (teklif, kabul) Ay 3'te gelecek.
// Her çağrı yeni bir karar kaydı yazar (kasıtlı): bir iş zamanla birçok kez değerlendirilir
// (ret gelince, yeni ekip boşa çıkınca) ve geçmiş silinmemeli. Bu yüzden fonksiyon tekrar çağrılınca aynı kaydı güncellemez.
public sealed class TeamRecommendationService
{
    private readonly AppDbContext _db;
    private readonly JobRequestLoader _jobLoader;
    private readonly TeamSnapshotLoader _snapshotLoader;
    private readonly ScoringTeamLoader _scoringLoader;
    private readonly IClock _clock;
    private readonly AssignmentSettings _settings;

    public TeamRecommendationService(
        AppDbContext db,
        JobRequestLoader jobLoader,
        TeamSnapshotLoader snapshotLoader,
        ScoringTeamLoader scoringLoader,
        IClock clock,
        IOptions<AssignmentSettings> settings)
    {
        _db = db;
        _jobLoader = jobLoader;
        _snapshotLoader = snapshotLoader;
        _scoringLoader = scoringLoader;
        _clock = clock;
        _settings = settings.Value;
    }

    public async Task<RecommendationResult> RecommendAsync(long jobId, CancellationToken cancellationToken)
    {
        // "Şimdi" bir kez alınır; yükleyiciler, eleme ve karar kaydı aynı anı kullanır.
        var now = _clock.UtcNow;

        // 1) İşi yükle
        var job = await _jobLoader.LoadAsync(jobId, now, cancellationToken);
        if (job is null)
        {
            return new RecommendationResult(RecommendationOutcome.JobNotFound, null, 0, 0);
        }

        // Sadece kuyruktaki iş için öneri yapılır; karar yazılmaz.
        if (job.Status != JobStatus.Queued)
        {
            return new RecommendationResult(RecommendationOutcome.JobNotQueued, null, 0, 0);
        }

        // 2) Ekip fotoğraflarını yükle, 3) ele
        var snapshots = await _snapshotLoader.LoadAsync(job.Location, now, cancellationToken);
        var elimination = TeamEliminator.Eliminate(job.Request, snapshots, now, _settings);

        // 4) Sadece adaylar için puanlama karnesi, 5) puanla ve sırala
        var scoringTeams = await _scoringLoader.LoadAsync(job.Request, elimination.Candidates, now, cancellationToken);
        var ranked = TeamScorer.Rank(scoringTeams, job.Request.Urgency, _settings);

        // 6) Karar günlüğüne yaz
        var distanceByTeam = snapshots.ToDictionary(s => s.TeamId, s => s.DistanceKm);

        var decision = new AssignmentDecision(
            jobId,
            job.Request.Urgency,
            _settings.GetWeightSet(job.Request.Urgency),
            now);

        foreach (var eliminated in elimination.Eliminated.OrderBy(e => e.TeamId))
        {
            decision.AddEliminated(eliminated.TeamId, distanceByTeam[eliminated.TeamId], eliminated.Reason);
        }

        for (var i = 0; i < ranked.Count; i++)
        {
            var team = ranked[i];
            decision.AddCandidate(team.TeamId, distanceByTeam[team.TeamId], team.Score, rank: i + 1);
        }

        // Karar ve tüm satırlar tek kayıt işleminde yazılır: yarım kayıt kalmaz.
        _db.AssignmentDecisions.Add(decision);
        await _db.SaveChangesAsync(cancellationToken);

        var outcome = ranked.Count == 0 ? RecommendationOutcome.NoCandidates : RecommendationOutcome.Recommended;
        return new RecommendationResult(outcome, decision.Id, ranked.Count, elimination.Eliminated.Count);
    }
}