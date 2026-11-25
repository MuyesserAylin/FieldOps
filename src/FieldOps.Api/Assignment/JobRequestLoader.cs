using FieldOps.Api.Data;
using FieldOps.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FieldOps.Api.Assignment;

// İş numarasından, ekip aramak için gereken iş paketini (AssignmentJob) hazırlar.
// SADECE OKUR: veritabanında hiçbir şeyi değiştirmez, karar vermez.
// Güncel aciliyet bellekte hesaplanır (bekleme yükseltmesi); iş satırına yazılmaz,
// böylece "ekip öner" salt okunur kalır. Şimdiki zaman dışarıdan gelir (saf mantık kuralı).
public sealed class JobRequestLoader
{
    private readonly AppDbContext _db;
    private readonly AssignmentSettings _settings;

    public JobRequestLoader(AppDbContext db, IOptions<AssignmentSettings> settings)
    {
        _db = db;
        _settings = settings.Value;
    }

    public async Task<AssignmentJob?> LoadAsync(long jobId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var row = await _db.Jobs
            .AsNoTracking()
            .Where(j => j.Id == jobId)
            .Select(j => new
            {
                j.Id,
                j.FaultType.RequiredSkillId,
                j.Urgency,
                j.InitialUrgency,
                j.CreatedAt,
                j.Status,
                j.Location
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var urgency = row.Urgency;
        if (row.Status == JobStatus.Queued)
        {
            var escalated = UrgencyEscalator.GetEffectiveUrgency(row.InitialUrgency, row.CreatedAt, now, _settings);
            urgency = escalated > urgency ? escalated : urgency;
        }

        var request = new JobRequest(row.Id, row.RequiredSkillId, urgency, new HashSet<int>());

        return new AssignmentJob(request, row.Location, row.Status);
    }
}