using FieldOps.Api.Data;
using FieldOps.Api.Domain;
using FieldOps.Api.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FieldOps.Api.Assignment;

public sealed class UrgencyEscalationService
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly AssignmentSettings _settings;
    private readonly ILogger<UrgencyEscalationService> _logger;

    public UrgencyEscalationService(
        AppDbContext db,
        IClock clock,
        IOptions<AssignmentSettings> settings,
        ILogger<UrgencyEscalationService> logger)
    {
        _db = db;
        _clock = clock;
        _settings = settings.Value;
        _logger = logger;
    }

    // Aciliyeti yükselen iş sayısını döndürür.
    public async Task<int> EscalateQueuedJobsAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var jobs = await _db.Jobs
            .Where(j => j.Status == JobStatus.Queued && j.Urgency < Urgency.High)
            .OrderBy(j => j.Id)
            .ToListAsync(cancellationToken);

        var escalatedCount = 0;

        foreach (var job in jobs)
        {
            var target = UrgencyEscalator.GetEffectiveUrgency(job.InitialUrgency, job.CreatedAt, now, _settings);

            if (!job.EscalateUrgency(target))
            {
                continue;
            }

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                escalatedCount++;
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning(
                    "İş {JobId} aciliyeti yükseltilirken başka bir değişiklikle çakıştı; bir sonraki turda tekrar denenecek.",
                    job.Id);
                _db.Entry(job).State = EntityState.Detached;
            }
        }

        return escalatedCount;
    }
}