using FieldOps.Api.Domain;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests;

public class TeamTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Point ValidLocation() => new(30.5206, 39.7767) { SRID = 4326 };

    private static Team CreateTeam() => new("Team A", ValidLocation(), Now);


    [Fact]
    public void NewTeam_StartsIdle_WithNoCooldown_AndEmptyCollections()
    {
        var team = CreateTeam();

        Assert.Equal("Team A", team.Name);
        Assert.Equal(TeamStatus.Idle, team.Status);
        Assert.Equal(Now, team.LocationUpdatedAt);
        Assert.Null(team.CooldownUntil);
        Assert.Empty(team.Skills);
        Assert.Empty(team.Shifts);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NewTeam_WithBlankName_Throws(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Team(name!, ValidLocation(), Now));
    }

    [Fact]
    public void NewTeam_WithNullLocation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Team("Team A", null!, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3857)]
    public void NewTeam_WithWrongSrid_Throws(int srid)
    {
        var location = new Point(30.5206, 39.7767) { SRID = srid };

        Assert.Throws<ArgumentException>(() => new Team("Team A", location, Now));
    }


    [Theory]
    [InlineData(TeamSkillLevel.Basic)]
    [InlineData(TeamSkillLevel.Expert)]
    public void AddSkill_WithValidLevel_AddsSkill(TeamSkillLevel level)
    {
        var team = CreateTeam();

        team.AddSkill(skillId: 3, level);

        var skill = Assert.Single(team.Skills);
        Assert.Equal(3, skill.SkillId);
        Assert.Equal(level, skill.Level);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void AddSkill_WithInvalidLevel_ThrowsAndAddsNothing(int level)
    {
        var team = CreateTeam();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            team.AddSkill(skillId: 3, (TeamSkillLevel)level));

        Assert.Empty(team.Skills);
    }

    [Fact]
    public void AddSkill_SameSkillTwice_Throws()
    {
        var team = CreateTeam();
        team.AddSkill(skillId: 3, TeamSkillLevel.Basic);

        Assert.Throws<InvalidOperationException>(() =>
            team.AddSkill(skillId: 3, TeamSkillLevel.Expert));

        var skill = Assert.Single(team.Skills);
        Assert.Equal(TeamSkillLevel.Basic, skill.Level);
    }

    [Fact]
    public void AddSkill_DifferentSkills_AddsBoth()
    {
        var team = CreateTeam();

        team.AddSkill(skillId: 1, TeamSkillLevel.Expert);
        team.AddSkill(skillId: 2, TeamSkillLevel.Basic);

        Assert.Equal(2, team.Skills.Count);
    }

    [Fact]
    public void AddShift_WithValidRange_AddsShift()
    {
        var team = CreateTeam();
        var start = Now;
        var end = Now.AddHours(8);

        team.AddShift(start, end);

        var shift = Assert.Single(team.Shifts);
        Assert.Equal(start, shift.StartsAt);
        Assert.Equal(end, shift.EndsAt);
    }

    [Fact]
    public void AddShift_OvernightShift_IsValid()
    {
        var team = CreateTeam();
        var start = new DateTimeOffset(2026, 1, 1, 22, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 2, 6, 0, 0, TimeSpan.Zero);

        team.AddShift(start, end);

        Assert.Single(team.Shifts);
    }

    [Theory]
    [InlineData(-60)]
    [InlineData(0)]
    public void AddShift_EndNotAfterStart_ThrowsAndAddsNothing(int minutesAfterStart)
    {
        var team = CreateTeam();

        Assert.Throws<ArgumentException>(() =>
            team.AddShift(Now, Now.AddMinutes(minutesAfterStart)));

        Assert.Empty(team.Shifts);
    }
}

