namespace FieldOps.Api.Domain;

public class JobStatusHistory
{
    // Sadece EF Core'un veritabanından satır okurken kullanması için
    private JobStatusHistory() { }

    public JobStatusHistory(
        Job job,
        JobStatus? oldStatus,
        JobStatus newStatus,
        DateTimeOffset changedAt)
    {
        ArgumentNullException.ThrowIfNull(job);

        Job = job;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        ChangedAt = changedAt;
    }

    public long Id { get; private set; }
    public long JobId { get; private set; }
    public Job Job { get; private set; } = null!;

    public JobStatus? OldStatus { get; private set; }
    public JobStatus NewStatus { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
}