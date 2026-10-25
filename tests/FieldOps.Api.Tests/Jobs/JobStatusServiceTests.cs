using FieldOps.Api.Domain;
using FieldOps.Api.Jobs;
using FieldOps.Api.Tests.Database;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests.Jobs;

[Collection("Database")]
[Trait("Category", "Integration")]
public class JobStatusServiceTests : IAsyncLifetime
{
    private readonly PostgisFixture _fixture;
    private readonly FakeClock _clock =
        new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    public JobStatusServiceTests(PostgisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<long> CreateJobAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var faultType = await db.FaultTypes.SingleAsync(f => f.Name == "Gas leak");
        var location = new Point(32.8597, 39.9334) { SRID = 4326 };
        var job = new Job(faultType, location, "Smell of gas", "STATUSCODE01", _clock.UtcNow);

        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }
    private async Task<JobStatusChangeResult> ChangeAsync(long jobId, JobStatus newStatus)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new JobStatusService(db, _clock);
        return await service.ChangeStatusAsync(jobId, newStatus, CancellationToken.None);
    }

    [Fact]
    public async Task ChangeStatus_WhenAnotherChangeWasSavedFirst_ReturnsConflictAndWritesNothing()
    {
        var jobId = await CreateJobAsync();

        await using var loserDb = _fixture.CreateDbContext();
        await using var winnerDb = _fixture.CreateDbContext();

        await loserDb.Jobs.SingleAsync(j => j.Id == jobId);

        var winnerService = new JobStatusService(winnerDb, _clock);
        var loserService = new JobStatusService(loserDb, _clock);

        var winnerResult = await winnerService.ChangeStatusAsync(
            jobId, JobStatus.Offered, CancellationToken.None);
        var loserResult = await loserService.ChangeStatusAsync(
            jobId, JobStatus.Cancelled, CancellationToken.None);

        Assert.Equal(JobStatusChangeOutcome.Changed, winnerResult.Outcome);
        Assert.Equal(JobStatusChangeOutcome.ConcurrencyConflict, loserResult.Outcome);

        await using var checkDb = _fixture.CreateDbContext();
        var job = await checkDb.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(JobStatus.Offered, job.Status);

        var history = await checkDb.JobStatusHistories.SingleAsync();
        Assert.Equal(JobStatus.Queued, history.OldStatus);
        Assert.Equal(JobStatus.Offered, history.NewStatus);
    }
    [Fact]
    public async Task ChangeStatus_ValidTransition_UpdatesJobAndWritesHistory()
    {
        var jobId = await CreateJobAsync();

        var result = await ChangeAsync(jobId, JobStatus.Offered);

        Assert.Equal(JobStatusChangeOutcome.Changed, result.Outcome);
        Assert.Equal(JobStatus.Queued, result.OldStatus);
        Assert.Equal(JobStatus.Offered, result.NewStatus);

        await using var checkDb = _fixture.CreateDbContext();
        var job = await checkDb.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(JobStatus.Offered, job.Status);

        var history = await checkDb.JobStatusHistories.SingleAsync();
        Assert.Equal(JobStatus.Queued, history.OldStatus);
        Assert.Equal(JobStatus.Offered, history.NewStatus);
        Assert.Equal(_clock.UtcNow, history.ChangedAt);
    }

    [Fact]
    public async Task ChangeStatus_FullPathToCompleted_WritesFourHistoryRowsInOrder()
    {
        var jobId = await CreateJobAsync();
        JobStatus[] path =
            [JobStatus.Offered, JobStatus.OnRoute, JobStatus.OnSite, JobStatus.Completed];

        foreach (var status in path)
        {
            var result = await ChangeAsync(jobId, status);
            Assert.Equal(JobStatusChangeOutcome.Changed, result.Outcome);
        }

        await using var checkDb = _fixture.CreateDbContext();
        var job = await checkDb.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(JobStatus.Completed, job.Status);

        var history = await checkDb.JobStatusHistories.OrderBy(h => h.Id).ToListAsync();
        Assert.Equal(4, history.Count);

        JobStatus?[] expectedOld =
            [JobStatus.Queued, JobStatus.Offered, JobStatus.OnRoute, JobStatus.OnSite];
        Assert.Equal(expectedOld, history.Select(h => h.OldStatus).ToArray());
        Assert.Equal(path, history.Select(h => h.NewStatus).ToArray());
    }

    [Fact]
    public async Task ChangeStatus_ForbiddenTransition_ReturnsInvalidAndWritesNothing()
    {
        var jobId = await CreateJobAsync();

        var result = await ChangeAsync(jobId, JobStatus.Completed);

        Assert.Equal(JobStatusChangeOutcome.InvalidTransition, result.Outcome);

        await using var checkDb = _fixture.CreateDbContext();
        var job = await checkDb.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(0, await checkDb.JobStatusHistories.CountAsync());
    }

    [Fact]
    public async Task ChangeStatus_FromCompletedJob_ReturnsInvalid()
    {
        var jobId = await CreateJobAsync();
        foreach (var status in new[]
                 { JobStatus.Offered, JobStatus.OnRoute, JobStatus.OnSite, JobStatus.Completed })
        {
            await ChangeAsync(jobId, status);
        }

        var result = await ChangeAsync(jobId, JobStatus.Queued);

        Assert.Equal(JobStatusChangeOutcome.InvalidTransition, result.Outcome);

        await using var checkDb = _fixture.CreateDbContext();
        var job = await checkDb.Jobs.SingleAsync(j => j.Id == jobId);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(4, await checkDb.JobStatusHistories.CountAsync());
    }

    [Fact]
    public async Task ChangeStatus_UnknownJobId_ReturnsNotFound()
    {
        var result = await ChangeAsync(999999, JobStatus.Offered);

        Assert.Equal(JobStatusChangeOutcome.NotFound, result.Outcome);

        await using var checkDb = _fixture.CreateDbContext();
        Assert.Equal(0, await checkDb.JobStatusHistories.CountAsync());
    }
}