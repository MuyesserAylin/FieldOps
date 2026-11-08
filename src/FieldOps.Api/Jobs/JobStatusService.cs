using System;
using System.Threading.Tasks.Dataflow;
using FieldOps.Api.Data;
using FieldOps.Api.Domain;
using FieldOps.Api.Time;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Jobs;

public sealed class JobStatusService
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public JobStatusService(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<JobStatusChangeResult> ChangeStatusAsync(
        long jobId, JobStatus newStatus, CancellationToken cancellationToken)
    {
        var job = await _db.Jobs
        .SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job is null)
        {
            return JobStatusChangeResult.NotFound();
        }

        var oldStatus = job.Status;
        JobStatusHistory history;

        try
        {
            history = job.TransitionTo(newStatus, _clock.UtcNow);
        }
        catch (InvalidJobTransitionException)
        {
            return JobStatusChangeResult.InvalidTransition(oldStatus, newStatus);
        }

        _db.JobStatusHistories.Add(history);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return JobStatusChangeResult.ConcurrencyConflict();

        }

        return JobStatusChangeResult.Changed(oldStatus, newStatus);

    }
}
