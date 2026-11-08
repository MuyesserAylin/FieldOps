using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FieldOps.Api.Domain;
using FieldOps.Api.Tests.Auth;
using FieldOps.Api.Tests.Database;
using FieldOps.Api.Tests.Reports;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests.Jobs;

[Collection("Database")]
[Trait("Category", "Integration")]
public class OperatorJobsApiTests : IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgisFixture _fixture;
    private readonly FieldOpsApiFactory _factory;

    public OperatorJobsApiTests(PostgisFixture fixture)
    {
        _fixture = fixture;
        _factory = new FieldOpsApiFactory(fixture.ConnectionString);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task AddJobAsync(
        string faultTypeName, string trackingCode, DateTimeOffset createdAt,
        JobStatus? moveTo = null)
    {
        await using var db = _fixture.CreateDbContext();
        var faultType = await db.FaultTypes.SingleAsync(f => f.Name == faultTypeName);
        var location = new Point(32.8597, 39.9334) { SRID = 4326 };
        var job = new Job(faultType, location, "Test job", trackingCode, createdAt);

        db.Jobs.Add(job);
        if (moveTo is not null)
        {
            db.JobStatusHistories.Add(job.TransitionTo(moveTo.Value, createdAt.AddMinutes(1)));
        }

        await db.SaveChangesAsync();
    }

    private async Task AddManyJobsAsync(int count)
    {
        await using var db = _fixture.CreateDbContext();
        var faultType = await db.FaultTypes.SingleAsync(f => f.Name == "Pothole");

        for (var i = 0; i < count; i++)
        {
            var location = new Point(32.8597, 39.9334) { SRID = 4326 };
            db.Jobs.Add(new Job(
                faultType, location, "Bulk job", $"BULK{i:D8}", T0.AddSeconds(i)));
        }

        await db.SaveChangesAsync();
    }

    private static async Task<List<JsonElement>> ReadItemsAsync(HttpResponseMessage response)
    {
        var root = await response.Content.ReadFromJsonAsync<JsonElement>();
        return root.EnumerateArray().ToList();
    }

    private Task<HttpClient> OperatorClientAsync()
        => AuthTestHelper.CreateClientAsync(_factory, _fixture, "fake-operator", UserRole.Operator);

    [Fact]
    public async Task List_WithoutToken_Returns401Problem()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/operator/jobs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task List_AsTeam_Returns403Problem()
    {
        var client = await AuthTestHelper.CreateClientAsync(
            _factory, _fixture, "fake-team", UserRole.Team);

        var response = await client.GetAsync("/api/operator/jobs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(UserRole.Operator)]
    [InlineData(UserRole.Admin)]
    public async Task List_AsOperatorOrAdmin_Returns200(UserRole role)
    {
        var client = await AuthTestHelper.CreateClientAsync(
            _factory, _fixture, "fake-staff", role);

        var response = await client.GetAsync("/api/operator/jobs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task List_SortsByUrgencyThenOldestFirst()
    {
        await AddJobAsync("Pothole", "SORTCODE0001", T0);                         // Medium, eski
        await AddJobAsync("Gas leak", "SORTCODE0002", T0.AddMinutes(10));         // Critical, yeni
        await AddJobAsync("Gas leak", "SORTCODE0003", T0.AddMinutes(1));          // Critical, eski
        await AddJobAsync("Burst water pipe", "SORTCODE0004", T0.AddMinutes(2));  // High
        var client = await OperatorClientAsync();

        var response = await client.GetAsync("/api/operator/jobs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var codes = (await ReadItemsAsync(response))
            .Select(i => i.GetProperty("trackingCode").GetString())
            .ToArray();

        Assert.Equal(
            new[] { "SORTCODE0003", "SORTCODE0002", "SORTCODE0004", "SORTCODE0001" },
            codes);
    }

    [Fact]
    public async Task List_StatusFilter_ReturnsOnlyThatStatus()
    {
        await AddJobAsync("Gas leak", "FILTCODE0001", T0);
        await AddJobAsync("Gas leak", "FILTCODE0002", T0.AddMinutes(1));
        await AddJobAsync("Gas leak", "FILTCODE0003", T0.AddMinutes(2), JobStatus.Offered);
        var client = await OperatorClientAsync();

        var response = await client.GetAsync("/api/operator/jobs?status=offered");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await ReadItemsAsync(response);
        var item = Assert.Single(items);
        Assert.Equal("FILTCODE0003", item.GetProperty("trackingCode").GetString());
        Assert.Equal("Offered", item.GetProperty("status").GetString());
    }

    [Fact]
    public async Task List_WithInvalidStatus_Returns400()
    {
        var client = await OperatorClientAsync();

        var response = await client.GetAsync("/api/operator/jobs?status=bogus");

        await ProblemAssert.HasFieldErrorAsync(response, "status");
    }

    [Fact]
    public async Task List_WithLimitZero_Returns400()
    {
        var client = await OperatorClientAsync();

        var response = await client.GetAsync("/api/operator/jobs?limit=0");

        await ProblemAssert.HasFieldErrorAsync(response, "limit");
    }

    [Fact]
    public async Task List_WithLimit_ReturnsAtMostThatMany()
    {
        await AddJobAsync("Gas leak", "LIMTCODE0001", T0);
        await AddJobAsync("Gas leak", "LIMTCODE0002", T0.AddMinutes(1));
        await AddJobAsync("Gas leak", "LIMTCODE0003", T0.AddMinutes(2));
        var client = await OperatorClientAsync();

        var response = await client.GetAsync("/api/operator/jobs?limit=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await ReadItemsAsync(response)).Count);
    }

    [Fact]
    public async Task List_LimitDefaultsTo50AndIsCappedAt100()
    {
        await AddManyJobsAsync(101);
        var client = await OperatorClientAsync();

        var defaultResponse = await client.GetAsync("/api/operator/jobs");
        var cappedResponse = await client.GetAsync("/api/operator/jobs?limit=1000");

        Assert.Equal(50, (await ReadItemsAsync(defaultResponse)).Count);
        Assert.Equal(100, (await ReadItemsAsync(cappedResponse)).Count);
    }

    [Fact]
    public async Task List_ReturnsExpectedFields()
    {
        await AddJobAsync("Gas leak", "FLDSCODE0001", T0);
        var client = await OperatorClientAsync();

        var response = await client.GetAsync("/api/operator/jobs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.Single(await ReadItemsAsync(response));

        Assert.True(item.GetProperty("id").GetInt64() > 0);
        Assert.Equal("FLDSCODE0001", item.GetProperty("trackingCode").GetString());
        Assert.Equal("Gas leak", item.GetProperty("faultType").GetString());
        Assert.Equal("Queued", item.GetProperty("status").GetString());
        Assert.Equal("Critical", item.GetProperty("urgency").GetString());
        Assert.Equal("Test job", item.GetProperty("description").GetString());
        Assert.Equal(39.9334, item.GetProperty("latitude").GetDouble(), 6);
        Assert.Equal(32.8597, item.GetProperty("longitude").GetDouble(), 6);
    }
}