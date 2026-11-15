using FieldOps.Api.Data;
using FieldOps.Api.Time;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Teams;

public sealed record OperatorTeamSkillItem(string Name, int Level);

public sealed record OperatorTeamItem(
    int Id,
    string Name,
    string Status,
    double Latitude,
    double Longitude,
    DateTimeOffset LocationUpdatedAt,
    bool OnShift,
    IReadOnlyList<OperatorTeamSkillItem> Skills);

public sealed class OperatorTeamService
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public OperatorTeamService(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<OperatorTeamItem>> ListAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;

        var rows = await _db.Teams
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Status,
                t.Location,
                t.LocationUpdatedAt,
                OnShift = t.Shifts.Any(s => s.StartsAt <= now && now < s.EndsAt),
                Skills = t.Skills
                    .OrderBy(ts => ts.Skill.Name)
                    .Select(ts => new { ts.Skill.Name, Level = (int)ts.Level })
                    .ToList()
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new OperatorTeamItem(
                r.Id,
                r.Name,
                r.Status.ToString(),
                r.Location.Y,
                r.Location.X,
                r.LocationUpdatedAt,
                r.OnShift,
                r.Skills.Select(s => new OperatorTeamSkillItem(s.Name, s.Level)).ToList()))
            .ToList();
    }
}