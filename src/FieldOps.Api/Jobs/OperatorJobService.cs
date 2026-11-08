
using FieldOps.Api.Data;
using FieldOps.Api.Domain;
using Microsoft.EntityFrameworkCore;


namespace FieldOps.Api.Jobs;

public sealed record OperatorJobItem(
    long Id,
    string TrackingCode,
    string FaultType,
    string Status,
    string Urgency,
    string Description,
    double Latitude,
    double Longitude,
    DateTimeOffset CreatedAt
);
public sealed class OperatorJobService
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    private readonly AppDbContext _db;

    public OperatorJobService(AppDbContext db)
    {
        _db = db;
    }

    //önce aciliyet sırasın gore aciliyet sırası aynıysa olusturulma zanaı eskı olana gore
    //olustrulma zamanıda aynıysa idye gore(idye gore determenistik)
    public async Task<IReadOnlyList<OperatorJobItem>> ListAsync(
        JobStatus? status, int limit, CancellationToken cancellationToken)
    {
        var query = _db.Jobs.AsNoTracking();

        if (status is not null)
        {
            var wanted = status.Value;
            query = query.Where(j => j.Status == wanted);
        }

        var rows = await query
            .OrderByDescending(j => j.Urgency)
            .ThenBy(j => j.CreatedAt)
            .ThenBy(j => j.Id)
            .Take(limit)
            .Select(j => new
            {
                j.Id,
                j.TrackingCode,
                FaultTypeName = j.FaultType.Name,
                j.Status,
                j.Urgency,
                j.Description,
                j.Location,
                j.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return rows
         .Select(r => new OperatorJobItem(
             r.Id,
             r.TrackingCode,
             r.FaultTypeName,
             r.Status.ToString(),
             r.Urgency.ToString(),
             r.Description,
             r.Location.Y,
             r.Location.X,
             r.CreatedAt))
         .ToList();
    }
}
