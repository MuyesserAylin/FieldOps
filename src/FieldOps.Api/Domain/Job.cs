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


    public void AssignTo(int teamId, DateTimeOffset assignedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(teamId);

        if (Status is not (JobStatus.OnRoute or JobStatus.OnSite))
        {
            throw new InvalidOperationException(
                $"Ekip yalnızca 'Yolda' veya 'İşte' durumundaki işe atanabilir (şu anki durum: {Status}).");
        }

        if (AssignedTeamId is not null)
        {
            throw new InvalidOperationException(
                $"Bu iş zaten {AssignedTeamId} numaralı ekip tarafından üstlenilmiş.");
        }

        AssignedTeamId = teamId;
        AssignedAt = assignedAt;
    }


    public void ClearAssignment()
    {
        if (Status == JobStatus.Completed)
        {
            throw new InvalidOperationException("Tamamlanan işin ekip kaydı silinemez; geçmiş korunur.");
        }

        AssignedTeamId = null;
        AssignedTeam = null;
        AssignedAt = null;
    }

    public bool EscalateUrgency(Urgency newUrgency)
    {
        if (Status != JobStatus.Queued)
        {
            throw new InvalidOperationException(
                $"Aciliyet yalnızca 'Kuyrukta' durumundaki işte yükseltilebilir (şu anki durum: {Status}).");
        }

        if (!Enum.IsDefined(newUrgency) || newUrgency > Urgency.High)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newUrgency), newUrgency, "Aciliyet beklemeyle en fazla 'yüksek'e yükseltilebilir.");
        }

        if (newUrgency <= Urgency)
        {
            return false;
        }

        Urgency = newUrgency;
        return true;
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
    public int? AssignedTeamId { get; private set; }
    public Team? AssignedTeam { get; private set; }
    public DateTimeOffset? AssignedAt { get; private set; }

    [Timestamp]
    public uint Version { get; private set; }
}