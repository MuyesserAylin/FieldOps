using FieldOps.Api.Assignment;
using FieldOps.Api.Domain;
using FieldOps.Api.Tests.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests.Assignment;

[Collection("Database")]
[Trait("Category", "Integration")]
public class UrgencyEscalationServiceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgisFixture _fixture;

    public UrgencyEscalationServiceTests(PostgisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<long> SaveJobAsync(
        int createdMinutesAgo,
        Urgency initialUrgency,
        JobStatus status = JobStatus.Queued)
    {
        var createdAt = Now.AddMinutes(-createdMinutesAgo);

        await using var db = _fixture.CreateDbContext();
        var faultType = await db.FaultTypes.FirstAsync(f => f.DefaultUrgency == Urgency.Low);
        var location = new Point(32.8597, 39.9334) { SRID = 4326 };
        var trackingCode = Guid.NewGuid().ToString("N")[..16];
        var job = new Job(faultType, location, "Escalation test", trackingCode, createdAt);

        if (status == JobStatus.OnRoute)
        {
            job.TransitionTo(JobStatus.Offered, createdAt);
            job.TransitionTo(JobStatus.OnRoute, createdAt);
        }

        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        if (initialUrgency != Urgency.Low)
        {
            await db.Database.ExecuteSqlAsync(
                $"UPDATE jobs SET initial_urgency = {(int)initialUrgency}, urgency = {(int)initialUrgency} WHERE id = {job.Id}");
        }

        return job.Id;
    }

    private async Task<int> RunServiceAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var service = new UrgencyEscalationService(
            db,
            new FakeClock(Now),
            Options.Create(TestSettings.Standard()),
            NullLogger<UrgencyEscalationService>.Instance);

        return await service.EscalateQueuedJobsAsync(CancellationToken.None);
    }

    private async Task<(Urgency Urgency, Urgency Initial)> ReadUrgenciesAsync(long jobId)
    {
        await using var db = _fixture.CreateDbContext();
        var job = await db.Jobs.AsNoTracking().SingleAsync(j => j.Id == jobId);
        return (job.Urgency, job.InitialUrgency);
    }

    [Theory]
    [InlineData(10, Urgency.Low, 0)]
    [InlineData(31, Urgency.Medium, 1)]
    [InlineData(61, Urgency.High, 1)]
    public async Task LowJob_IsEscalatedByWaitingTime_AndInitialUrgencyStays(
        int createdMinutesAgo, Urgency expectedUrgency, int expectedCount)
    {
        var jobId = await SaveJobAsync(createdMinutesAgo, Urgency.Low);

        var count = await RunServiceAsync();

        var (urgency, initial) = await ReadUrgenciesAsync(jobId);
        Assert.Equal(expectedCount, count);
        Assert.Equal(expectedUrgency, urgency);
        Assert.Equal(Urgency.Low, initial);
    }

    [Fact]
    public async Task MediumJob_Waiting31Minutes_BecomesHigh()
    {
        var jobId = await SaveJobAsync(31, Urgency.Medium);

        var count = await RunServiceAsync();

        var (urgency, initial) = await ReadUrgenciesAsync(jobId);
        Assert.Equal(1, count);
        Assert.Equal(Urgency.High, urgency);
        Assert.Equal(Urgency.Medium, initial);
    }

    [Theory]
    [InlineData(Urgency.High)]
    [InlineData(Urgency.Critical)]
    public async Task HighAndCriticalJobs_AreNeverChanged_HoweverOldTheyAre(Urgency initialUrgency)
    {
        var jobId = await SaveJobAsync(600, initialUrgency);

        var count = await RunServiceAsync();

        var (urgency, initial) = await ReadUrgenciesAsync(jobId);
        Assert.Equal(0, count);
        Assert.Equal(initialUrgency, urgency);
        Assert.Equal(initialUrgency, initial);
    }

    [Fact]
    public async Task JobNotInQueue_IsNotChanged()
    {
        var jobId = await SaveJobAsync(61, Urgency.Low, JobStatus.OnRoute);

        var count = await RunServiceAsync();

        var (urgency, _) = await ReadUrgenciesAsync(jobId);
        Assert.Equal(0, count);
        Assert.Equal(Urgency.Low, urgency);
    }

    [Fact]
    public async Task RunningTwice_SecondRunChangesNothing()
    {
        var jobId = await SaveJobAsync(61, Urgency.Low);

        var first = await RunServiceAsync();
        var second = await RunServiceAsync();

        var (urgency, _) = await ReadUrgenciesAsync(jobId);
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(Urgency.High, urgency);
    }

    [Fact]
    public async Task ReturnedCount_IsTheNumberOfJobsActuallyEscalated()
    {
        var fresh = await SaveJobAsync(10, Urgency.Low);
        var waited = await SaveJobAsync(31, Urgency.Low);
        var longWaited = await SaveJobAsync(61, Urgency.Low);
        var critical = await SaveJobAsync(600, Urgency.Critical);

        var count = await RunServiceAsync();

        Assert.Equal(2, count);
        Assert.Equal(Urgency.Low, (await ReadUrgenciesAsync(fresh)).Urgency);
        Assert.Equal(Urgency.Medium, (await ReadUrgenciesAsync(waited)).Urgency);
        Assert.Equal(Urgency.High, (await ReadUrgenciesAsync(longWaited)).Urgency);
        Assert.Equal(Urgency.Critical, (await ReadUrgenciesAsync(critical)).Urgency);
    }
}