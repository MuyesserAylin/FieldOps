using System.ComponentModel.DataAnnotations;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Domain;

public class Job
{
    // Sadece EF Core'un veritabanından satır okurken kullanması için
    private Job() { }

    public Job(
        FaultType faultType,
        Point location,
        string description,
        string trackingCode,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(faultType);
        ArgumentNullException.ThrowIfNull(location);

        FaultType = faultType;
        FaultTypeId = faultType.Id;
        Urgency = faultType.DefaultUrgency;
        InitialUrgency = faultType.DefaultUrgency;
        Location = location;
        Description = description;
        TrackingCode = trackingCode;
        CreatedAt = createdAt;
        Status = JobStatus.Queued;
    }

    public JobStatusHistory TransitionTo(JobStatus newStatus, DateTimeOffset changedAt)
    {
        if (!JobStatusTransitions.IsAllowed(Status, newStatus))
        {
            throw new InvalidJobTransitionException(Status, newStatus);
        }

        var oldStatus = Status;
        Status = newStatus;

        return new JobStatusHistory(this, oldStatus, newStatus, changedAt);
    }

    public long Id { get; private set; }
    public int FaultTypeId { get; private set; }
    public FaultType FaultType { get; private set; } = null!;
    public Point Location { get; private set; } = null!;
    public string Description { get; private set; } = string.Empty;
    public Urgency InitialUrgency { get; private set; }
    public Urgency Urgency { get; private set; }
    public JobStatus Status { get; private set; }
    public string TrackingCode { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    [Timestamp]
    public uint Version { get; private set; }
}