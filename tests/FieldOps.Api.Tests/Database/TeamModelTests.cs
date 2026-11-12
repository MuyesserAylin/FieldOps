using FieldOps.Api.Domain;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Npgsql;

namespace FieldOps.Api.Tests.Database;

[Collection("Database")]
[Trait("Category", "Integration")]
public class TeamModelTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private const int ElectricalSkillId = 3;
    private const int RoadWorkSkillId = 4;

    private readonly PostgisFixture _fixture;

    public TeamModelTests(PostgisFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Team CreateTeam(string name = "Team A") =>
        new(name, new Point(30.5206, 39.7767) { SRID = 4326 }, Now);

    private async Task<int> SaveTeamAsync(string name)
    {
        await using var db = _fixture.CreateDbContext();
        var team = CreateTeam(name);
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team.Id;
    }

    [Fact]
    public async Task SavedTeam_ReadBackWithNewContext_KeepsLocationSkillsAndShifts()
    {
        var shiftStart = Now;
        var shiftEnd = Now.AddHours(8);
        int teamId;

        await using (var writeDb = _fixture.CreateDbContext())
        {
            var team = CreateTeam();
            team.AddSkill(ElectricalSkillId, TeamSkillLevel.Expert);
            team.AddSkill(RoadWorkSkillId, TeamSkillLevel.Basic);
            team.AddShift(shiftStart, shiftEnd);

            writeDb.Teams.Add(team);
            await writeDb.SaveChangesAsync();
            teamId = team.Id;
        }

        await using var readDb = _fixture.CreateDbContext();
        var loaded = await readDb.Teams
            .Include(t => t.Skills)
            .Include(t => t.Shifts)
            .SingleAsync(t => t.Id == teamId);

        Assert.Equal("Team A", loaded.Name);
        Assert.Equal(30.5206, loaded.Location.X, 6);
        Assert.Equal(39.7767, loaded.Location.Y, 6);
        Assert.Equal(4326, loaded.Location.SRID);
        Assert.Equal(TeamStatus.Idle, loaded.Status);
        Assert.Equal(Now, loaded.LocationUpdatedAt);
        Assert.Null(loaded.CooldownUntil);

        Assert.Equal(2, loaded.Skills.Count);
        Assert.Equal(TeamSkillLevel.Expert, loaded.Skills.Single(s => s.SkillId == ElectricalSkillId).Level);
        Assert.Equal(TeamSkillLevel.Basic, loaded.Skills.Single(s => s.SkillId == RoadWorkSkillId).Level);

        var shift = Assert.Single(loaded.Shifts);
        Assert.Equal(shiftStart, shift.StartsAt);
        Assert.Equal(shiftEnd, shift.EndsAt);
    }

    [Fact]
    public async Task StatusIsStoredAsText()
    {
        var teamId = await SaveTeamAsync("Team A");

        await using var db = _fixture.CreateDbContext();
        var rawStatus = await db.Database
            .SqlQuery<string>($"SELECT status AS \"Value\" FROM teams WHERE id = {teamId}")
            .SingleAsync();

        Assert.Equal("Idle", rawStatus);
    }

    [Fact]
    public async Task SecondTeamWithSameName_ThrowsDbUpdateException()
    {
        await SaveTeamAsync("Same Name");

        await using var db = _fixture.CreateDbContext();
        db.Teams.Add(CreateTeam("Same Name"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }


    [Fact]
    public async Task Database_RejectsSkillLevelOutsideOneToThree()
    {
        var teamId = await SaveTeamAsync("Team A");

        await using var db = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync(
                $"INSERT INTO team_skills (team_id, skill_id, level) VALUES ({teamId}, {ElectricalSkillId}, {5})"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Theory]
    [InlineData(-60)] // bitiş başlangıçtan önce
    [InlineData(0)]   // bitiş = başlangıç
    public async Task Database_RejectsShiftThatDoesNotEndAfterItStarts(int minutesAfterStart)
    {
        var teamId = await SaveTeamAsync("Team A");
        var endsAt = Now.AddMinutes(minutesAfterStart);

        await using var db = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync(
                $"INSERT INTO shifts (team_id, starts_at, ends_at) VALUES ({teamId}, {Now}, {endsAt})"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task Database_RejectsSameSkillTwiceForSameTeam()
    {
        int teamId;

        await using (var writeDb = _fixture.CreateDbContext())
        {
            var team = CreateTeam();
            team.AddSkill(ElectricalSkillId, TeamSkillLevel.Expert);
            writeDb.Teams.Add(team);
            await writeDb.SaveChangesAsync();
            teamId = team.Id;
        }

        await using var db = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync(
                $"INSERT INTO team_skills (team_id, skill_id, level) VALUES ({teamId}, {ElectricalSkillId}, {2})"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }
}