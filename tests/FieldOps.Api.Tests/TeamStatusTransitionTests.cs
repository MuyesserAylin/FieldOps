using FieldOps.Api.Domain;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests;

public class TeamStatusTransitionTests
{
    private static readonly (TeamStatus From, TeamStatus To)[] AllowedPairs =
    [
        (TeamStatus.Idle, TeamStatus.OfferPending),
        (TeamStatus.Idle, TeamStatus.Offline),
        (TeamStatus.OfferPending, TeamStatus.OnRoute),
        (TeamStatus.OfferPending, TeamStatus.Idle),
        (TeamStatus.OfferPending, TeamStatus.Offline),
        (TeamStatus.OnRoute, TeamStatus.OnSite),
        (TeamStatus.OnRoute, TeamStatus.Idle),
        (TeamStatus.OnRoute, TeamStatus.Offline),
        (TeamStatus.OnSite, TeamStatus.Idle),
        (TeamStatus.OnSite, TeamStatus.Offline),
        (TeamStatus.Offline, TeamStatus.Idle),
    ];

    public static TheoryData<TeamStatus, TeamStatus> AllowedTransitions
    {
        get
        {
            var data = new TheoryData<TeamStatus, TeamStatus>();
            foreach (var (from, to) in AllowedPairs)
            {
                data.Add(from, to);
            }

            return data;
        }
    }

    public static TheoryData<TeamStatus, TeamStatus> ForbiddenTransitions
    {
        get
        {
            var data = new TheoryData<TeamStatus, TeamStatus>();
            foreach (var from in Enum.GetValues<TeamStatus>())
            {
                foreach (var to in Enum.GetValues<TeamStatus>())
                {
                    if (!AllowedPairs.Contains((from, to)))
                    {
                        data.Add(from, to);
                    }
                }
            }

            return data;
        }
    }

    private static Team CreateTeamInStatus(TeamStatus status)
    {
        var location = new Point(30.5206, 39.7767) { SRID = 4326 };
        var team = new Team("Team A", location, DateTimeOffset.UnixEpoch);

        switch (status)
        {
            case TeamStatus.Idle:
                break;
            case TeamStatus.OfferPending:
                team.TransitionTo(TeamStatus.OfferPending);
                break;
            case TeamStatus.OnRoute:
                team.TransitionTo(TeamStatus.OfferPending);
                team.TransitionTo(TeamStatus.OnRoute);
                break;
            case TeamStatus.OnSite:
                team.TransitionTo(TeamStatus.OfferPending);
                team.TransitionTo(TeamStatus.OnRoute);
                team.TransitionTo(TeamStatus.OnSite);
                break;
            case TeamStatus.Offline:
                team.TransitionTo(TeamStatus.Offline);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        return team;
    }

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void TransitionTo_AllowedTransition_UpdatesStatus(TeamStatus from, TeamStatus to)
    {
        var team = CreateTeamInStatus(from);

        team.TransitionTo(to);

        Assert.Equal(to, team.Status);
    }

    [Theory]
    [MemberData(nameof(ForbiddenTransitions))]
    public void TransitionTo_ForbiddenTransition_ThrowsAndKeepsStatus(TeamStatus from, TeamStatus to)
    {
        var team = CreateTeamInStatus(from);

        var ex = Assert.Throws<InvalidTeamTransitionException>(() => team.TransitionTo(to));

        Assert.Equal(from, ex.From);
        Assert.Equal(to, ex.To);
        Assert.Equal(from, team.Status);
    }

    [Fact]
    public void AllowedPlusForbidden_CoversAll25Pairs()
    {
        var total = Enum.GetValues<TeamStatus>().Length;

        Assert.Equal(5, total);
        Assert.Equal(11, AllowedTransitions.Count());
        Assert.Equal(14, ForbiddenTransitions.Count());
        Assert.Equal(total * total, AllowedTransitions.Count() + ForbiddenTransitions.Count());
    }
}
