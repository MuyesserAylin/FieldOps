using System.Net;
using System.Net.Http.Json;
using FieldOps.Api.Domain;
using FieldOps.Api.Reports;
using FieldOps.Api.Tests.Database;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Tests.Reports;

[Collection("Database")]
[Trait("Category", "Integration")]
public class ReportsApiTests : IAsyncLifetime
{
    private readonly PostgisFixture _fixture;
    private readonly FieldOpsApiFactory _factory;

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
}