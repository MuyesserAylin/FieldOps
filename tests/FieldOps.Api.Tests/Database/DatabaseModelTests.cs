using FieldOps.Api.Domain;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests.Database;

[Collection("Database")]
[Trait("Category", "Integration")]
public class DatabaseModelTests : IAsyncLifetime
{
    private readonly PostgisFixture _fixture;

    public DatabaseModelTests(PostgisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migrations_SeedSkillsAndFaultTypes()
    {
        await using var db = _fixture.CreateDbContext();

        Assert.Equal(4, await db.Skills.CountAsync());
        Assert.Equal(5, await db.FaultTypes.CountAsync());

        var gasLeak = await db.FaultTypes
            .Include(f => f.RequiredSkill)
            .SingleAsync(f => f.Name == "Gas leak");

        Assert.Equal("Gas", gasLeak.RequiredSkill.Name);
        Assert.Equal(Urgency.Critical, gasLeak.DefaultUrgency);
    }

    [Fact]
    public async Task SavedJob_ReadBackWithNewContext_KeepsLocationAndFields()
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        long jobId;

        await using (var writeDb = _fixture.CreateDbContext())
        {
            var faultType = await writeDb.FaultTypes.SingleAsync(f => f.Name == "Gas leak");
            var location = new Point(32.8597, 39.9334) { SRID = 4326 };
            var job = new Job(faultType, location, "Smell of gas", "TESTCODE0001", createdAt);

            writeDb.Jobs.Add(job);
            await writeDb.SaveChangesAsync();
            jobId = job.Id;
        }

        await using var readDb = _fixture.CreateDbContext();
        var loaded = await readDb.Jobs.SingleAsync(j => j.Id == jobId);

        Assert.Equal(32.8597, loaded.Location.X, 6);
        Assert.Equal(39.9334, loaded.Location.Y, 6);
        Assert.Equal(4326, loaded.Location.SRID);
        Assert.Equal(JobStatus.Queued, loaded.Status);
        Assert.Equal(Urgency.Critical, loaded.InitialUrgency);
        Assert.Equal(Urgency.Critical, loaded.Urgency);
        Assert.Equal(createdAt, loaded.CreatedAt);
    }
    [Fact]
    public async Task SecondJobWithSameTrackingCode_ThrowsDbUpdateException()
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        await using (var firstDb = _fixture.CreateDbContext())
        {
            var faultType = await firstDb.FaultTypes.SingleAsync(f => f.Name == "Gas leak");
            var location = new Point(32.8597, 39.9334) { SRID = 4326 };

            firstDb.Jobs.Add(new Job(faultType, location, "First report", "SAMECODE0001", createdAt));
            await firstDb.SaveChangesAsync();
        }

        await using var secondDb = _fixture.CreateDbContext();
        var otherFaultType = await secondDb.FaultTypes.SingleAsync(f => f.Name == "Gas leak");
        var otherLocation = new Point(32.8597, 39.9334) { SRID = 4326 };

        secondDb.Jobs.Add(new Job(otherFaultType, otherLocation, "Second report", "SAMECODE0001", createdAt));

        await Assert.ThrowsAsync<DbUpdateException>(() => secondDb.SaveChangesAsync());
    }
    [Fact]
    public async Task StatusIsStoredAsText_AndUrgencyIsStoredAsNumber()
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        long jobId;

        await using (var writeDb = _fixture.CreateDbContext())
        {
            var faultType = await writeDb.FaultTypes.SingleAsync(f => f.Name == "Gas leak");
            var location = new Point(32.8597, 39.9334) { SRID = 4326 };
            var job = new Job(faultType, location, "Smell of gas", "RAWCODE00001", createdAt);

            writeDb.Jobs.Add(job);
            await writeDb.SaveChangesAsync();
            jobId = job.Id;
        }

        await using var readDb = _fixture.CreateDbContext();

        var rawStatus = await readDb.Database
            .SqlQuery<string>($"SELECT status AS \"Value\" FROM jobs WHERE id = {jobId}")
            .SingleAsync();

        var rawUrgency = await readDb.Database
            .SqlQuery<int>($"SELECT urgency AS \"Value\" FROM jobs WHERE id = {jobId}")
            .SingleAsync();

        Assert.Equal("Queued", rawStatus);
        Assert.Equal(4, rawUrgency);
    }
}