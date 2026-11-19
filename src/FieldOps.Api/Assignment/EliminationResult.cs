namespace FieldOps.Api.Assignment;

public sealed record EliminationResult(
    IReadOnlyList<TeamSnapshot> Candidates,    //tüm adaylar
    IReadOnlyList<EliminatedTeam> Eliminated); //elenenler
