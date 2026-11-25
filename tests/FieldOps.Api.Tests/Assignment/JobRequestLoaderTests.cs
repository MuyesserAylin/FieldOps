using FieldOps.Api.Assignment;
using FieldOps.Api.Domain;
using FieldOps.Api.Tests.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests.Assignment;

[Collection("Database")]
[Trait("Category", "Integration")]
public class JobRequestLoaderTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgisFixture _fixture;

    public JobRequestLoaderTests(PostgisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static string FaultTypeNameFor(Urgency urgency) => urgency switch
    {
        Urgency.Low => "Street light failure",
        Urgency.Medium => "Pothole",
        Urgency.High => "Burst water pipe",
        Urgency.Critical => "Gas leak",
        _ => throw new ArgumentOutOfRangeException(nameof(urgency))
    };

    private async Task<long> SaveJobAsync(
        int createdMinutesAgo,
        Urgency initialUrgency,
        JobStatus status = JobStatus.Queued)
    {
        var createdAt = Now.AddMinutes(-createdMinutesAgo);

        await using var db = _fixture.CreateDbContext();
        var faultTypeName = FaultTypeNameFor(initialUrgency);
        var faultType = await db.FaultTypes.SingleAsync(f => f.Name == faultTypeName);
        var location = new Point(32.8597, 39.9334) { SRID = 4326 };
        var trackingCode = Guid.NewGuid().ToString("N")[..16];
        var job = new Job(faultType, location, "Loader test", trackingCode, createdAt);

        if (status == JobStatus.OnRoute)
        {
            job.TransitionTo(JobStatus.Offered, createdAt);
            job.TransitionTo(JobStatus.OnRoute, createdAt);
        }

        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    private async Task<AssignmentJob?> LoadAsync(long jobId)
    {
        await using var db = _fixture.CreateDbContext();
        var loader = new JobRequestLoader(db, Options.Create(TestSettings.Standard()));
        return await loader.LoadAsync(jobId, Now, CancellationToken.None);
    }

    [Fact]
    public async Task MissingJob_ReturnsNull()
    {
        Assert.Null(await LoadAsync(999_999));
    }

    [Fact]
    public async Task LoadedJob_CarriesSkillLocationStatusAndNoRejectedTeams()
    {
        var jobId = await SaveJobAsync(5, Urgency.Low);

        var loaded = await LoadAsync(jobId);

        Assert.NotNull(loaded);
        Assert.Equal(jobId, loaded.Request.JobId);
        Assert.Equal(3, loaded.Request.RequiredSkillId);
        Assert.Equal(Urgency.Low, loaded.Request.Urgency);
        Assert.Empty(loaded.Request.RejectedTeamIds);
        Assert.Equal(JobStatus.Queued, loaded.Status);
        Assert.Equal(32.8597, loaded.Location.X, 6);
        Assert.Equal(39.9334, loaded.Location.Y, 6);
    }

    [Theory]
    [InlineData(Urgency.Low, 10, Urgency.Low)]
    [InlineData(Urgency.Low, 31, Urgency.Medium)]
    [InlineData(Urgency.Low, 61, Urgency.High)]
    [InlineData(Urgency.Medium, 31, Urgency.High)]
    [InlineData(Urgency.High, 600, Urgency.High)]
    [InlineData(Urgency.Critical, 600, Urgency.Critical)]
    public async Task QueuedJob_GetsEffectiveUrgencyFromWaitingTime(
        Urgency initialUrgency, int createdMinutesAgo, Urgency expected)
    {
        var jobId = await SaveJobAsync(createdMinutesAgo, initialUrgency);

        var loaded = await LoadAsync(jobId);

        Assert.Equal(expected, loaded!.Request.Urgency);
    }

    [Fact]
    public async Task Loader_DoesNotWriteTheEscalatedUrgencyToTheDatabase()
    {
        var jobId = await SaveJobAsync(61, Urgency.Low);

        var loaded = await LoadAsync(jobId);

        Assert.Equal(Urgency.High, loaded!.Request.Urgency);

        await using var db = _fixture.CreateDbContext();
        var stored = await db.Jobs.AsNoTracking().SingleAsync(j => j.Id == jobId);
        Assert.Equal(Urgency.Low, stored.Urgency);
        Assert.Equal(Urgency.Low, stored.InitialUrgency);
    }

    [Fact]
    public async Task JobNotInQueue_IsNotEscalated()
    {
        var jobId = await SaveJobAsync(61, Urgency.Low, JobStatus.OnRoute);

        var loaded = await LoadAsync(jobId);

        Assert.Equal(Urgency.Low, loaded!.Request.Urgency);
        Assert.Equal(JobStatus.OnRoute, loaded.Status);
    }

    [Fact]
    public async Task UrgencyRaisedInTheRow_IsNotLostWhenWaitingTimeGivesLess()
    {
        var jobId = await SaveJobAsync(5, Urgency.Low);
        await using (var db = _fixture.CreateDbContext())
        {
            await db.Database.ExecuteSqlAsync($"UPDATE jobs SET urgency = {(int)Urgency.High} WHERE id = {jobId}");
        }

        var loaded = await LoadAsync(jobId);

        Assert.Equal(Urgency.High, loaded!.Request.Urgency);
    }
}