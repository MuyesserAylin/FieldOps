using FieldOps.Api.Data;
using FieldOps.Api.Domain;
using FieldOps.Api.Time;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using static FieldOps.Api.Domain.TeamSkillLevel;

namespace FieldOps.Api.Teams;

public sealed class DemoTeamSeeder
{
    private enum ShiftKind { Day, Night }

    //demo ekibi tarifi
    private sealed record TeamSpec(
        string Name,
        double Latitude,
        double Longitude,
        (string Skill, TeamSkillLevel Level)[] Skills,
        ShiftKind Shift,
        bool Offline = false);

    private static readonly string[] SkillNames = ["Gas", "Plumbing", "Electrical", "RoadWork"];
    private const int ShiftDays = 45; //dünden başlayarak kaç günlük vardiya yazılacak
    private static readonly TimeSpan LocalOffset = TimeSpan.FromHours(3);

    private static readonly TeamSpec[] Specs =
    [
        new("Gas Team A", 39.7767, 30.5206, [("Gas", Expert), ("Plumbing", Basic)], ShiftKind.Day),
        new("Gas Team B", 39.8082, 30.4559, [("Gas", Intermediate)], ShiftKind.Day),
        new("Gas Team C", 39.7407, 30.5735, [("Gas", Expert)], ShiftKind.Night),
        new("Gas Team D", 39.8217, 30.5559, [("Gas", Intermediate), ("Plumbing", Basic)], ShiftKind.Night),
        new("Gas Team E", 39.7407, 30.4265, [("Gas", Intermediate)], ShiftKind.Day),
        new("Gas Team F", 39.8217, 30.6147, [("Gas", Basic)], ShiftKind.Night),

        new("Plumbing Team A", 39.7632, 30.5382, [("Plumbing", Expert), ("RoadWork", Basic)], ShiftKind.Night),
        new("Plumbing Team B", 39.7317, 30.4735, [("Plumbing", Intermediate)], ShiftKind.Day),
        new("Plumbing Team C", 39.7947, 30.5912, [("Plumbing", Expert)], ShiftKind.Day),
        new("Plumbing Team D", 39.8308, 30.4853, [("Plumbing", Intermediate)], ShiftKind.Night),
        new("Plumbing Team E", 39.7497, 30.6265, [("Plumbing", Basic)], ShiftKind.Day),
        new("Plumbing Team F", 39.7947, 30.4147, [("Plumbing", Intermediate)], ShiftKind.Night),

        new("Electrical Team A", 39.7947, 30.5088, [("Electrical", Intermediate), ("Gas", Basic)], ShiftKind.Day),
        new("Electrical Team B", 39.8127, 30.5794, [("Electrical", Expert)], ShiftKind.Day),
        new("Electrical Team C", 39.7497, 30.4618, [("Electrical", Expert)], ShiftKind.Night),
        new("Electrical Team D", 39.7226, 30.5441, [("Electrical", Intermediate)], ShiftKind.Night),
        new("Electrical Team E", 39.8217, 30.4382, [("Electrical", Basic)], ShiftKind.Day),
        new("Electrical Team F", 39.7362, 30.6147, [("Electrical", Expert)], ShiftKind.Night),

        new("RoadWork Team A", 39.7812, 30.5500, [("RoadWork", Expert)], ShiftKind.Day),
        new("RoadWork Team B", 39.7587, 30.4500, [("RoadWork", Intermediate)], ShiftKind.Day, Offline: true),
        new("RoadWork Team C", 39.8262, 30.5677, [("RoadWork", Expert)], ShiftKind.Night),
        new("RoadWork Team D", 39.7181, 30.5088, [("RoadWork", Intermediate)], ShiftKind.Night),
        new("RoadWork Team E", 39.8127, 30.6265, [("RoadWork", Basic)], ShiftKind.Day),
        new("RoadWork Team F", 39.7317, 30.4206, [("RoadWork", Intermediate)], ShiftKind.Night),
    ];

    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;

    public DemoTeamSeeder(AppDbContext db, IConfiguration configuration, IClock clock)
    {
        _db = db;
        _configuration = configuration;
        _clock = clock;
    }

    public async Task<bool> SeedAsync(CancellationToken cancellationToken)
    {
        if (!bool.TryParse(_configuration["Seed:DemoTeams"], out var enabled) || !enabled)
        {
            return false;
        }

        if (await _db.Teams.AnyAsync(cancellationToken))
        {
            return false;
        }

        var skillIds = await LoadSkillIdsAsync(cancellationToken);
        var now = _clock.UtcNow;

        // Dünden başlıyoruz: gece 03:00'te çalışırsa şu an geçerli gece vardiyası dün 20:00'de başlamıştır
        var firstDay = now.ToOffset(LocalOffset).Date.AddDays(-1);

        foreach (var spec in Specs)
        {
            var location = new Point(spec.Longitude, spec.Latitude) { SRID = 4326 };
            var team = new Team(spec.Name, location, now);

            foreach (var (skill, level) in spec.Skills)
            {
                team.AddSkill(skillIds[skill], level);
            }

            AddShifts(team, spec.Shift, firstDay);

            if (spec.Offline)
            {
                team.TransitionTo(TeamStatus.Offline);
            }

            _db.Teams.Add(team);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void AddShifts(Team team, ShiftKind kind, DateTime firstDay)
    {
        for (var i = 0; i < ShiftDays; i++)
        {
            var day = firstDay.AddDays(i);

            var (start, end) = kind == ShiftKind.Day
                ? (day.AddHours(8), day.AddHours(20))
                : (day.AddHours(20), day.AddDays(1).AddHours(8));

            team.AddShift(
                new DateTimeOffset(start, LocalOffset).ToUniversalTime(),
                new DateTimeOffset(end, LocalOffset).ToUniversalTime());
        }
    }

    private async Task<Dictionary<string, int>> LoadSkillIdsAsync(CancellationToken cancellationToken)
    {
        var found = await _db.Skills
            .Where(s => SkillNames.Contains(s.Name))
            .ToDictionaryAsync(s => s.Name, s => s.Id, cancellationToken);

        foreach (var name in SkillNames)
        {
            if (!found.ContainsKey(name))
            {
                throw new InvalidOperationException(
                    $"Demo teams need the skill '{name}', but it was not found in the database.");
            }
        }

        return found;
    }
}