using System;
using FieldOps.Api.Data;
using FieldOps.Api.Domain;
using FieldOps.Api.Time;
using FieldOps.Api.Tracking;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Npgsql;

namespace FieldOps.Api.Reports;

public class ReportService
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly ITrackingCodeGenerator _trackingCodes;
    private const int MaxAttempts = 3;


    public ReportService(AppDbContext db,
    IClock clock,
    ITrackingCodeGenerator trackingCodeGenerator)
    {
        _db = db;
        _clock = clock;
        _trackingCodes = trackingCodeGenerator;
    }

    public async Task<CreateReportResponse?> CreateAsync(
       ValidatedReport report, CancellationToken cancellationToken)
    {
        var faultType = await _db.FaultTypes
            .SingleOrDefaultAsync(f => f.Id == report.FaultTypeId, cancellationToken);

        if (faultType is null)
        {
            return null;
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var now = _clock.UtcNow;
            var location = new Point(report.Longitude, report.Latitude) { SRID = 4326 };
            var trackingCode = _trackingCodes.Generate();

            var job = new Job(faultType, location, report.Description, trackingCode, now);
            var history = new JobStatusHistory(job, null, JobStatus.Queued, now);

            _db.Add(job);
            _db.Add(history);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);

                return new CreateReportResponse(
                    job.TrackingCode,
                    job.Status.ToString(),
                    faultType.Name,
                    job.CreatedAt);
            }
            catch (DbUpdateException ex) when (IsTrackingCodeCollision(ex))
            {
                _db.Entry(history).State = EntityState.Detached;
                _db.Entry(job).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException(
            $"Could not generate a unique tracking code after {MaxAttempts} attempts.");
    }

    public async Task<ReportStatusResponse?> GetStatusAsync(
    string? trackingCode, CancellationToken cancellationToken)
    {
        var normalized = TrackingCodeNormalizer.Normalize(trackingCode);
        if (normalized is null)
        {
            return null;
        }

        var job = await _db.Jobs
            .AsNoTracking()
            .Where(j => j.TrackingCode == normalized)
            .Select(j => new
            {
                j.TrackingCode,
                j.Status,
                FaultTypeName = j.FaultType.Name,
                j.Description,
                j.CreatedAt,
                History = _db.JobStatusHistories
                    .Where(h => h.JobId == j.Id)
                    .OrderBy(h => h.ChangedAt)
                    .ThenBy(h => h.Id)
                    .Select(h => new { h.NewStatus, h.ChangedAt })
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            return null;
        }

        var historyItems = job.History
            .Select(h => new ReportHistoryItem(h.NewStatus.ToString(), h.ChangedAt))
            .ToList();

        return new ReportStatusResponse(
            job.TrackingCode,
            job.Status.ToString(),
            job.FaultTypeName,
            job.Description,
            job.CreatedAt,
            historyItems);
    }

    private static bool IsTrackingCodeCollision(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ix_jobs_tracking_code"
        };
    }
}