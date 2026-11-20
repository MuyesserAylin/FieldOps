using System.Globalization;
using FieldOps.Api.Assignment;
using FieldOps.Api.Domain;
using FieldOps.Api.Teams;
using FieldOps.Api.Tests.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetTopologySuite.Geometries;
using Xunit.Abstractions;
using static FieldOps.Api.Tests.Assignment.EliminationTestData;

namespace FieldOps.Api.Tests.Assignment;

// Eleme fonksiyonunu seeder'ın gerçek 24 ekibiyle çalıştırır.
// Saat donduruldu: seeder de eleme de aynı "şimdi"yi kullanır (15 Kasım 2026, 12:00 yerel).
[Collection("Database")]
[Trait("Category", "Integration")]
public class RealSeedEliminationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);

    private const int GasSkillId = 1;
    private const int RoadWorkSkillId = 4;

    // Şehrin ortası.
    private static readonly Point JobLocation =
        new(ServiceArea.CenterLongitude, ServiceArea.CenterLatitude) { SRID = 4326 };

    private readonly PostgisFixture _fixture;
    private readonly ITestOutputHelper _output;

    public RealSeedEliminationTests(PostgisFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Dictionary<int, string>> SeedAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DemoTeams"] = "true" })
            .Build();

        await using var db = _fixture.CreateDbContext();

        var seeded = await new DemoTeamSeeder(db, configuration, new FakeClock(Now))
            .SeedAsync(CancellationToken.None);
        Assert.True(seeded);

        return await db.Teams.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name);
    }

    private async Task<(Dictionary<int, string> Names, EliminationResult Result)> RunAsync(
        string title, JobRequest job, DateTimeOffset now)
    {
        var names = await SeedAsync();

        await using var db = _fixture.CreateDbContext();
        var snapshots = await new TeamSnapshotLoader(db)
            .LoadAsync(JobLocation, now, CancellationToken.None);

        var result = TeamEliminator.Eliminate(job, snapshots, now, TestSettings.Standard());

        Print(title, names, result);
        return (names, result);
    }

    private void Print(string title, Dictionary<int, string> names, EliminationResult result)
    {
        _output.WriteLine($"=== {title} ===");

        foreach (var team in result.Candidates)
        {
            _output.WriteLine(
                $"ADAY    {names[team.TeamId],-18} {team.DistanceKm.ToString("F1", CultureInfo.InvariantCulture)} km");
        }

        foreach (var eliminated in result.Eliminated.OrderBy(e => e.Reason).ThenBy(e => names[e.TeamId], StringComparer.Ordinal))
        {
            _output.WriteLine($"ELENDİ  {names[eliminated.TeamId],-18} {eliminated.Reason}");
        }
    }

    private static string[] CandidateNames(EliminationResult result, Dictionary<int, string> names)
        => result.Candidates.Select(t => names[t.TeamId]).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    private static string[] NamesWith(EliminationResult result, Dictionary<int, string> names, EliminationReason reason)
        => result.Eliminated
            .Where(e => e.Reason == reason)
            .Select(e => names[e.TeamId])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public async Task Low_urgency_street_light_in_city_center()
    {
        var (names, result) = await RunAsync(
            "Sokak lambası (düşük, elektrik, sınır 4 km)",
            Job(requiredSkillId: ElectricalSkillId, urgency: Urgency.Low),
            Now);

        Assert.Equal(new[] { "Electrical Team A" }, CandidateNames(result, names));
        Assert.Equal(
            new[] { "Electrical Team B", "Electrical Team E" },
            NamesWith(result, names, EliminationReason.TooFar));
        Assert.Equal(
            new[] { "Electrical Team C", "Electrical Team D", "Electrical Team F" },
            NamesWith(result, names, EliminationReason.NotOnShift));
        Assert.Equal(18, NamesWith(result, names, EliminationReason.MissingSkill).Length);
        Assert.Equal(24, result.Candidates.Count + result.Eliminated.Count);
    }

    [Fact]
    public async Task Critical_gas_leak_in_city_center_has_no_distance_limit()
    {
        var (names, result) = await RunAsync(
            "Gaz kaçağı (kritik, doğalgaz, mesafe sınırı yok)",
            Job(requiredSkillId: GasSkillId, urgency: Urgency.Critical),
            Now);

        Assert.Equal(
            new[] { "Electrical Team A", "Gas Team A", "Gas Team B", "Gas Team E" },
            CandidateNames(result, names));
        Assert.Equal(
            new[] { "Gas Team C", "Gas Team D", "Gas Team F" },
            NamesWith(result, names, EliminationReason.NotOnShift));
        Assert.Empty(NamesWith(result, names, EliminationReason.TooFar));
        Assert.Equal(17, NamesWith(result, names, EliminationReason.MissingSkill).Length);

        var gasE = result.Candidates.Single(t => names[t.TeamId] == "Gas Team E");
        Assert.InRange(gasE.DistanceKm, 8.5, 9.5);
    }

    [Fact]
    public async Task Medium_pothole_shows_offline_team_and_distance_limit()
    {
        var (names, result) = await RunAsync(
            "Çukur (orta, yol, sınır 8 km)",
            Job(requiredSkillId: RoadWorkSkillId, urgency: Urgency.Medium),
            Now);

        Assert.Equal(new[] { "RoadWork Team A" }, CandidateNames(result, names));
        Assert.Equal(new[] { "RoadWork Team B" }, NamesWith(result, names, EliminationReason.NotIdle));
        Assert.Equal(new[] { "RoadWork Team E" }, NamesWith(result, names, EliminationReason.TooFar));
        Assert.Equal(
            new[] { "Plumbing Team A", "RoadWork Team C", "RoadWork Team D", "RoadWork Team F" },
            NamesWith(result, names, EliminationReason.NotOnShift));
        Assert.Equal(17, NamesWith(result, names, EliminationReason.MissingSkill).Length);
    }

    [Theory]
    [InlineData(60, 1, 0)]
    [InlineData(61, 0, 3)]
    [InlineData(600, 0, 3)]
    public async Task Location_goes_stale_after_60_seconds(int secondsLater, int expectedCandidates, int expectedStale)
    {
        var (names, result) = await RunAsync(
            $"Sokak lambası, konum {secondsLater} sn eski",
            Job(requiredSkillId: ElectricalSkillId, urgency: Urgency.Low),
            Now.AddSeconds(secondsLater));

        Assert.Equal(expectedCandidates, result.Candidates.Count);
        Assert.Equal(expectedStale, NamesWith(result, names, EliminationReason.StaleLocation).Length);
    }
}