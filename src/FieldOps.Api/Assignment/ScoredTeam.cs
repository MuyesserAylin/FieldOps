namespace FieldOps.Api.Assignment;
//Puanlanmış ekip: hangi ekip, hangi notları aldı.
public sealed record ScoredTeam(
    int TeamId,
    ScoreBreakdown Score);

