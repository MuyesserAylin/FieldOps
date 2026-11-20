using FieldOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Assignment;

// Veritabanındaki ekipleri, eleme fonksiyonunun istediği "fotoğraflara" (TeamSnapshot) çevirir.
// Mesafeyi PostGIS hesaplar (kuşuçuşu, km). Şimdiki zamanı dışarıdan alır.
public sealed class TeamSnapshotLoader
{
    private readonly AppDbContext _db;

    public TeamSnapshotLoader(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TeamSnapshot>> LoadAsync(
        Point jobLocation,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jobLocation);

        if (jobLocation.SRID != 4326)
        {
            throw new ArgumentException("İş konumunun SRID'si 4326 olmalı.", nameof(jobLocation));
        }

        var rows = await _db.Teams
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => new
            {
                t.Id,
                t.Status,
                DistanceMeters = t.Location.Distance(jobLocation),
                t.LocationUpdatedAt,
                t.CooldownUntil,
                SkillIds = t.Skills.Select(s => s.SkillId).ToList(),
                Shifts = t.Shifts
                    .Where(s => s.EndsAt > now)
                    .Select(s => new { s.StartsAt, s.EndsAt })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new TeamSnapshot(
                r.Id,
                r.Status,
                r.DistanceMeters / 1000.0,
                r.LocationUpdatedAt,
                r.CooldownUntil,
                r.SkillIds,
                r.Shifts.Select(s => new ShiftWindow(s.StartsAt, s.EndsAt)).ToList()))
            .ToList();
    }
}