using System;

namespace FieldOps.Api.Domain;

public static class TeamStatusTransitions
{
    private static readonly HashSet<(TeamStatus From, TeamStatus To)> Allowed =
    [
        (TeamStatus.Idle,TeamStatus.OfferPending), 
        (TeamStatus.Idle,TeamStatus.Offline),
        (TeamStatus.OfferPending,TeamStatus.OnRoute),
        (TeamStatus.OfferPending,TeamStatus.Idle), 
        (TeamStatus.OfferPending,TeamStatus.Offline),
        (TeamStatus.OnRoute, TeamStatus.OnSite),
        (TeamStatus.OnRoute, TeamStatus.Idle),
        (TeamStatus.OnRoute, TeamStatus.Offline),
        (TeamStatus.OnSite, TeamStatus.Idle),
        (TeamStatus.OnSite, TeamStatus.Offline),
        (TeamStatus.Offline, TeamStatus.Idle),
];

    public static bool IsAllowed(TeamStatus from, TeamStatus to)
    => Allowed.Contains((from, to));


}
