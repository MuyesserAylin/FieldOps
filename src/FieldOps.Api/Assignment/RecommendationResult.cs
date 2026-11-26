namespace FieldOps.Api.Assignment;

// Ekip önerisinin sonucu.
public enum RecommendationOutcome
{
    Recommended,   // En az bir aday var; sırası 1 olan ekip önerilir.
    NoCandidates,  // Hiçbir ekip aday değil; iş kuyrukta bekler. Karar yine de günlüğe yazılır.
    JobNotFound,   // Böyle bir iş yok.
    JobNotQueued   // İş kuyrukta değil (teklifte, yolda, tamamlanmış...); öneri yapılmaz, karar yazılmaz.
}

// DecisionId: yazılan karar kaydının numarası (JobNotFound ve JobNotQueued'da null).
// CandidateCount / EliminatedCount: karardaki aday ve elenen ekip sayısı (JobNotFound ve JobNotQueued'da 0).
public sealed record RecommendationResult(
    RecommendationOutcome Outcome,
    long? DecisionId,
    int CandidateCount,
    int EliminatedCount);