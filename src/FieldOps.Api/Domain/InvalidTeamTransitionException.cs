namespace FieldOps.Api.Domain;

public sealed class InvalidTeamTransitionException : InvalidOperationException
{
    public TeamStatus From { get; }
    public TeamStatus To { get; }

    public InvalidTeamTransitionException(TeamStatus from, TeamStatus to)
        : base($"Team status cannot change from {from} to {to}.")
    {
        From = from;
        To = to;
    }
}