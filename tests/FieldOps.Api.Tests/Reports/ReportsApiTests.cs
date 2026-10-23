using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FieldOps.Api.Domain;
using FieldOps.Api.Reports;
using FieldOps.Api.Tests.Database;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests.Reports;

[Collection("Database")]
[Trait("Category", "Integration")]
public class ReportsApiTests : IAsyncLifetime
{
    private readonly PostgisFixture _fixture;
    private readonly FieldOpsApiFactory _factory;
    public static TheoryData<string, string> InvalidRequests => new()
    {
        { "longitude", """{"faultTypeId":1,"latitude":39.9334,"longitude":200,"description":"Smell of gas"}""" },
        { "latitude", """{"faultTypeId":1,"longitude":32.8597,"description":"Smell of gas"}""" },
        { "description", """{"faultTypeId":1,"latitude":39.9334,"longitude":32.8597,"description":"   "}""" },
        { "description", $$"""{"faultTypeId":1,"latitude":39.9334,"longitude":32.8597,"description":"{{new string('a', 2001)}}"}""" },
        { "faultTypeId", """{"faultTypeId":999,"latitude":39.9334,"longitude":32.8597,"description":"Smell of gas"}""" },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task PostReport_WithInvalidRequest_Returns400AndSavesNothing(
        string expectedField, string json)
    {
        var client = _factory.CreateClient();
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/reports", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedField, body, StringComparison.OrdinalIgnoreCase);

        await using var db = _fixture.CreateDbContext();
        Assert.Equal(0, await db.Jobs.CountAsync());
        Assert.Equal(0, await db.JobStatusHistories.CountAsync());
    }

    public ReportsApiTests(PostgisFixture fixture)
    {
        _fixture = fixture;
        _factory = new FieldOpsApiFactory(fixture.ConnectionString);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task PostReport_WithValidRequest_Returns201AndSavesJobWithHistory()
    {
        var client = _factory.CreateClient();
        var request = new
        {
            faultTypeId = 1,
            latitude = 39.9334,
            longitude = 32.8597,
            description = "Smell of gas"
        };

        var response = await client.PostAsJsonAsync("/api/reports", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateReportResponse>();
        Assert.NotNull(body);
        Assert.Equal(12, body.TrackingCode.Length);
        Assert.Equal("Queued", body.Status);
        Assert.Equal("Gas leak", body.FaultType);
        Assert.EndsWith(body.TrackingCode, response.Headers.Location!.ToString());

        await using var db = _fixture.CreateDbContext();
        var job = await db.Jobs.SingleAsync();
        var history = await db.JobStatusHistories.SingleAsync();

        Assert.Equal(body.TrackingCode, job.TrackingCode);
        Assert.Equal(32.8597, job.Location.X, 6);
        Assert.Equal(39.9334, job.Location.Y, 6);
        Assert.Equal(job.Id, history.JobId);
        Assert.Null(history.OldStatus);
        Assert.Equal(JobStatus.Queued, history.NewStatus);
    }
    [Fact]
    public async Task PostReport_WithLatitudeOutOfRange_Returns400AndSavesNothing()
    {
        var client = _factory.CreateClient();
        var request = new
        {
            faultTypeId = 1,
            latitude = 95,
            longitude = 32.8597,
            description = "Smell of gas"
        };

        var response = await client.PostAsJsonAsync("/api/reports", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("latitude", body, StringComparison.OrdinalIgnoreCase);

        await using var db = _fixture.CreateDbContext();
        Assert.Equal(0, await db.Jobs.CountAsync());
        Assert.Equal(0, await db.JobStatusHistories.CountAsync());
    }
    private static async Task<CreateReportResponse> CreateReportAsync(HttpClient client)
    {
        var request = new
        {
            faultTypeId = 1,
            latitude = 39.9334,
            longitude = 32.8597,
            description = "Smell of gas"
        };

        var response = await client.PostAsJsonAsync("/api/reports", request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<CreateReportResponse>();
        return body!;
    }
    [Fact]
    public async Task GetReport_WithCorrectCode_Returns200WithStatusAndHistory()
    {
        var client = _factory.CreateClient();
        var created = await CreateReportAsync(client);

        var response = await client.GetAsync($"/api/reports/{created.TrackingCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ReportStatusResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.TrackingCode, body.TrackingCode);
        Assert.Equal("Queued", body.Status);
        Assert.Equal("Gas leak", body.FaultType);
        Assert.Equal("Smell of gas", body.Description);

        var historyItem = Assert.Single(body.History);
        Assert.Equal("Queued", historyItem.Status);
    }
    private static string ApplyVariant(string code, string variant)
    {
        return variant switch
        {
            "lowercase" => code.ToLowerInvariant(),
            "dashed" => $"{code[..4]}-{code[4..8]}-{code[8..]}",
            "spaced" => Uri.EscapeDataString($"{code[..4]} {code[4..8]} {code[8..]}"),
            _ => throw new ArgumentOutOfRangeException(nameof(variant))
        };
    }
    [Theory]
    [InlineData("lowercase")]
    [InlineData("dashed")]
    [InlineData("spaced")]
    public async Task GetReport_WithDifferentlyWrittenCode_FindsSameJob(string variant)
    {
        var client = _factory.CreateClient();
        var created = await CreateReportAsync(client);
        var writtenCode = ApplyVariant(created.TrackingCode, variant);

        var response = await client.GetAsync($"/api/reports/{writtenCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ReportStatusResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.TrackingCode, body.TrackingCode);
    }
    [Theory]
    [InlineData("SHORT")]
    [InlineData("ABCDEFGHJKMNP")]
    [InlineData("ILOUILOUILOU")]
    [InlineData("AAAAAAAAAAAA")]
    [InlineData("0123456789AB")]
    public async Task GetReport_WithMalformedOrUnknownCode_Returns404(string code)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/reports/{code}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("not found", body, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task PostReport_WhenTrackingCodeCollides_RetriesWithNewCodeAndReturns201()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        await using (var seedDb = _fixture.CreateDbContext())
        {
            var faultType = await seedDb.FaultTypes.SingleAsync(f => f.Id == 1);
            var location = new Point(32.8597, 39.9334) { SRID = 4326 };

            seedDb.Jobs.Add(new Job(faultType, location, "Existing report", "AAAAAAAAAAAA", now));
            await seedDb.SaveChangesAsync();
        }

        var fakeGenerator = new SequenceTrackingCodeGenerator("AAAAAAAAAAAA", "BBBBBBBBBBBB");
        await using var factory = new FieldOpsApiFactory(_fixture.ConnectionString, fakeGenerator);
        var client = factory.CreateClient();

        var request = new
        {
            faultTypeId = 1,
            latitude = 39.9334,
            longitude = 32.8597,
            description = "Smell of gas"
        };

        var response = await client.PostAsJsonAsync("/api/reports", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateReportResponse>();
        Assert.NotNull(body);
        Assert.Equal("BBBBBBBBBBBB", body.TrackingCode);

        await using var db = _fixture.CreateDbContext();
        Assert.Equal(2, await db.Jobs.CountAsync());
        Assert.Equal(1, await db.JobStatusHistories.CountAsync());
    }
    [Fact]
    public async Task ResponsesDoNotExposeInternalIdOrCoordinates()
    {
        var client = _factory.CreateClient();
        var request = new
        {
            faultTypeId = 1,
            latitude = 39.9334,
            longitude = 32.8597,
            description = "Smell of gas"
        };

        var postResponse = await client.PostAsJsonAsync("/api/reports", request);
        postResponse.EnsureSuccessStatusCode();
        var postBody = await postResponse.Content.ReadAsStringAsync();

        var trackingCode = JsonDocument.Parse(postBody)
            .RootElement.GetProperty("trackingCode").GetString();

        var getResponse = await client.GetAsync($"/api/reports/{trackingCode}");
        getResponse.EnsureSuccessStatusCode();
        var getBody = await getResponse.Content.ReadAsStringAsync();

        foreach (var body in new[] { postBody, getBody })
        {
            Assert.DoesNotContain("\"id\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"jobId\"", body, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("latitude", getBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("longitude", getBody, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task GetReport_WithMultipleHistoryRows_ReturnsAllInOrder()
    {
        var client = _factory.CreateClient();
        var created = await CreateReportAsync(client);

        var start = DateTimeOffset.UtcNow;
        JobStatus[] path =
        [
            JobStatus.Offered,
            JobStatus.Queued,
            JobStatus.Offered,
            JobStatus.OnRoute,
            JobStatus.OnSite
        ];

        await using (var db = _fixture.CreateDbContext())
        {
            var job = await db.Jobs.SingleAsync(j => j.TrackingCode == created.TrackingCode);

            for (var i = 0; i < path.Length; i++)
            {
                var history = job.TransitionTo(path[i], start.AddMinutes(i + 1));
                db.JobStatusHistories.Add(history);
            }

            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/reports/{created.TrackingCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ReportStatusResponse>();
        Assert.NotNull(body);
        Assert.Equal("OnSite", body.Status);

        string[] expectedStatuses = ["Queued", "Offered", "Queued", "Offered", "OnRoute", "OnSite"];
        Assert.Equal(expectedStatuses, body.History.Select(h => h.Status).ToArray());

        var times = body.History.Select(h => h.ChangedAt).ToList();
        Assert.Equal(times.OrderBy(t => t).ToList(), times);
    }

}
