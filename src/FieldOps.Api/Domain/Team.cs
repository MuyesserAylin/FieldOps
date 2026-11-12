using NetTopologySuite.Geometries;

namespace FieldOps.Api.Domain;

public class Team
{

    private readonly List<TeamSkill> _skills = new();
    private readonly List<Shift> _shifts = new();

    // Sadece EF Core için
    private Team() { }

    public Team(string name, Point location, DateTimeOffset locationUpdatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(location);

        if (location.SRID != 4326)
        {
            throw new ArgumentException("Konum SRID'si 4326 olmalı.", nameof(location));
        }

        Name = name;
        Location = location;
        LocationUpdatedAt = locationUpdatedAt;
        Status = TeamStatus.Idle;
        CooldownUntil = null;
    }

    public void AddSkill(int skillId, TeamSkillLevel level)
    {

        var skill = new TeamSkill(skillId, level);

        if (_skills.Any(s => s.SkillId == skillId))
        {
            throw new InvalidOperationException("Bu ekipte bu beceri zaten var.");
        }

        _skills.Add(skill);
    }

    public void AddShift(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        _shifts.Add(new Shift(startsAt, endsAt));
    }

    public void TransitionTo(TeamStatus newStatus)
    {
        if (!TeamStatusTransitions.IsAllowed(Status, newStatus))
        {
            throw new InvalidTeamTransitionException(Status, newStatus);
        }
        Status = newStatus;
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public TeamStatus Status { get; private set; }
    public Point Location { get; private set; } = null!;
    public DateTimeOffset LocationUpdatedAt { get; private set; } //konum 60 snyden eski mi
    public DateTimeOffset? CooldownUntil { get; private set; } // zaman aşımı sonrası bekletme bitişi

    public IReadOnlyCollection<TeamSkill> Skills => _skills;
    public IReadOnlyCollection<Shift> Shifts => _shifts;
}