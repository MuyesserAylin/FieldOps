using FieldOps.Api.Domain;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Npgsql;

namespace FieldOps.Api.Tests.Database;

[Collection("Database")]
[Trait("Category", "Integration")]
public class JobAssignmentDatabaseTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgisFixture _fixture;

    public JobAssignmentDatabaseTests(PostgisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<int> SaveTeamAsync(string name = "Team A")
    {
        await using var db = _fixture.CreateDbContext();
        var team = new Team(name, new Point(30.5206, 39.7767) { SRID = 4326 }, Now);
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team.Id;
    }

    private async Task<long> SaveOnRouteJobAsync(int? assignToTeamId)
    {
        await using var db = _fixture.CreateDbContext();
        var faultType = await db.FaultTypes.SingleAsync(f => f.Name == "Gas leak");
        var location = new Point(32.8597, 39.9334) { SRID = 4326 };
        var job = new Job(faultType, location, "Smell of gas", "ASSIGNTEST01", Now);
        job.TransitionTo(JobStatus.Offered, Now);
        job.TransitionTo(JobStatus.OnRoute, Now);

        if (assignToTeamId is { } teamId)
        {
            job.AssignTo(teamId, Now.AddMinutes(5));
        }

        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    [Fact]
    public async Task AssignedJob_ReadBackWithNewContext_KeepsTeamAndTime()
    {
        var teamId = await SaveTeamAsync();
        var jobId = await SaveOnRouteJobAsync(teamId);

        await using var db = _fixture.CreateDbContext();
        var loaded = await db.Jobs.SingleAsync(j => j.Id == jobId);

        Assert.Equal(teamId, loaded.AssignedTeamId);
        Assert.Equal(Now.AddMinutes(5), loaded.AssignedAt);
    }

    [Fact]
    public async Task UnassignedJob_ReadBackWithNewContext_HasEmptyFields()
    {
        var jobId = await SaveOnRouteJobAsync(null);

        await using var db = _fixture.CreateDbContext();
        var loaded = await db.Jobs.SingleAsync(j => j.Id == jobId);

        Assert.Null(loaded.AssignedTeamId);
        Assert.Null(loaded.AssignedAt);
    }

    [Fact]
    public async Task ClearedAssignment_IsSavedAsEmptyFields()
    {
        var teamId = await SaveTeamAsync();
        var jobId = await SaveOnRouteJobAsync(teamId);

        await using (var writeDb = _fixture.CreateDbContext())
        {
            var job = await writeDb.Jobs.SingleAsync(j => j.Id == jobId);
            job.ClearAssignment();
            await writeDb.SaveChangesAsync();
        }

        await using var readDb = _fixture.CreateDbContext();
        var loaded = await readDb.Jobs.SingleAsync(j => j.Id == jobId);

        Assert.Null(loaded.AssignedTeamId);
        Assert.Null(loaded.AssignedAt);
    }


    [Fact]
    public async Task Database_RejectsTeamWithoutAssignedTime()
    {
        var teamId = await SaveTeamAsync();
        var jobId = await SaveOnRouteJobAsync(null);

        await using var db = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync(
                $"UPDATE jobs SET assigned_team_id = {teamId} WHERE id = {jobId}"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task Database_RejectsAssignedTimeWithoutTeam()
    {
        var jobId = await SaveOnRouteJobAsync(null);

        await using var db = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync(
                $"UPDATE jobs SET assigned_at = {Now} WHERE id = {jobId}"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task Database_RejectsDeletingTeamThatHasAnAssignedJob()
    {
        var teamId = await SaveTeamAsync();
        await SaveOnRouteJobAsync(teamId);

        await using var db = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM teams WHERE id = {teamId}"));

        Assert.True(
            ex.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation,
            $"Beklenmeyen hata kodu: {ex.SqlState}");
    }

    [Fact]
    public async Task Database_AllowsDeletingTeamWithoutAssignedJobs()
    {
        var teamId = await SaveTeamAsync();
        await SaveOnRouteJobAsync(null);

        await using var db = _fixture.CreateDbContext();
        var deletedRows = await db.Database.ExecuteSqlAsync($"DELETE FROM teams WHERE id = {teamId}");

        Assert.Equal(1, deletedRows);
    }
}