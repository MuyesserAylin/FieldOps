using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FieldOps.Api.Domain;
using FieldOps.Api.Tests.Auth;
using FieldOps.Api.Tests.Database;
using FieldOps.Api.Tests.Reports;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Tests.Jobs;

[Collection("Database")]
[Trait("Category", "Integration")]
public class OperatorStatusApiTests : IAsyncLifetime
{
    private const string CancelBody = "{\"status\":\"Cancelled\"}";

    private readonly PostgisFixture _fixture;
    private readonly FieldOpsApiFactory _factory;

    public OperatorStatusApiTests(PostgisFixture fixture)
    {
        _fixture = fixture;
        _factory = new FieldOpsApiFactory(fixture.ConnectionString);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    // Vatandaş gibi herkese açık adresten bir bildirim açar, işin numarasını ve takip kodunu döner.
    private async Task<(long Id, string TrackingCode)> CreateReportedJobAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/reports", new
        {
            faultTypeId = 1,
            latitude = 39.9334,
            longitude = 32.8597,
            description = "Smell of gas"
        });
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var code = json.RootElement.GetProperty("trackingCode").GetString()!;

        await using var db = _fixture.CreateDbContext();
        var job = await db.Jobs.SingleAsync(j => j.TrackingCode == code);
        return (job.Id, code);
    }

    private static Task<HttpResponseMessage> PatchStatusAsync(
        HttpClient client, string jobId, string bodyJson)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/operator/jobs/{jobId}/status")
        {
            Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
        };
        return client.SendAsync(request);
    }

    [Fact]
    public async Task ChangeStatus_WithoutToken_Returns401Problem()
    {
        var client = _factory.CreateClient();

        var response = await PatchStatusAsync(client, "1", CancelBody);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ChangeStatus_AsTeam_Returns403ProblemAndJobIsUnchanged()
    {
        var (id, _) = await CreateReportedJobAsync();
        var client = await AuthTestHelper.CreateClientAsync(
            _factory, _fixture, "fake-team", UserRole.Team);

        var response = await PatchStatusAsync(client, id.ToString(), CancelBody);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        await using var db = _fixture.CreateDbContext();
        var job = await db.Jobs.SingleAsync(j => j.Id == id);
        Assert.Equal(JobStatus.Queued, job.Status);
    }

    [Theory]
    [InlineData(UserRole.Operator, "Cancelled")]
    [InlineData(UserRole.Admin, "cancelled")]
    public async Task ChangeStatus_Cancel_Returns200AndSavesCancelledWithHistory(
        UserRole role, string statusText)
    {
        var (id, _) = await CreateReportedJobAsync();
        var client = await AuthTestHelper.CreateClientAsync(
            _factory, _fixture, "fake-staff", role);

        var response = await PatchStatusAsync(
            client, id.ToString(), $"{{\"status\":\"{statusText}\"}}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(id, json.RootElement.GetProperty("id").GetInt64());
        Assert.Equal("Queued", json.RootElement.GetProperty("oldStatus").GetString());
        Assert.Equal("Cancelled", json.RootElement.GetProperty("newStatus").GetString());

        await using var db = _fixture.CreateDbContext();
        var job = await db.Jobs.SingleAsync(j => j.Id == id);
        Assert.Equal(JobStatus.Cancelled, job.Status);

        // Birinci satır bildirim açılırken yazıldı (boş -> Queued), ikincisi iptal.
        var history = await db.JobStatusHistories.OrderBy(h => h.Id).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal(JobStatus.Queued, history[1].OldStatus);
        Assert.Equal(JobStatus.Cancelled, history[1].NewStatus);
    }

    [Fact]
    public async Task ChangeStatus_Cancel_CitizenSeesCancelledAndTwoHistoryRows()
    {
        var (id, trackingCode) = await CreateReportedJobAsync();
        var operatorClient = await AuthTestHelper.CreateClientAsync(
            _factory, _fixture, "fake-operator", UserRole.Operator);

        var cancel = await PatchStatusAsync(operatorClient, id.ToString(), CancelBody);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var citizenClient = _factory.CreateClient();
        var response = await citizenClient.GetAsync($"/api/reports/{trackingCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Cancelled", json.RootElement.GetProperty("status").GetString());

        var statuses = json.RootElement.GetProperty("history").EnumerateArray()
            .Select(h => h.GetProperty("status").GetString())
            .ToArray();
        Assert.Equal(new[] { "Queued", "Cancelled" }, statuses);
    }

    [Fact]
    public async Task ChangeStatus_ToStatusOperatorMayNotSet_Returns400AndJobIsUnchanged()
    {
        var (id, _) = await CreateReportedJobAsync();
        var client = await AuthTestHelper.CreateClientAsync(
            _factory, _fixture, "fake-operator", UserRole.Operator);

        var response = await PatchStatusAsync(client, id.ToString(), "{\"status\":\"Offered\"}");

        await ProblemAssert.HasFieldErrorAsync(response, "status");

        await using var db = _fixture.CreateDbContext();
        var job = await db.Jobs.SingleAsync(j => j.Id == id);
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(1, await db.JobStatusHistories.CountAsync());
    }

    [Theory]
    [InlineData("{\"status\":\"Done\"}")]
    [InlineData("{\"status\":\"5\"}")]
    [InlineData("{\"status\":\"\"}")]
    [InlineData("{}")]
    public async Task ChangeStatus_WithBadStatusInBody_Returns400(string bodyJson)
    {
        var (id, _) = await CreateReportedJobAsync();
        var client = await AuthTestHelper.CreateClientAsync(
            _factory, _fixture, "fake-operator", UserRole.Operator);

        var response = await PatchStatusAsync(client, id.ToString(), bodyJson);

        await ProblemAssert.HasFieldErrorAsync(response, "status");
    }

    [Fact]
    public async Task ChangeStatus_UnknownJob_Returns404Problem()
    {
        var client = await AuthTestHelper.CreateClientAsync(
            _factory, _fixture, "fake-operator", UserRole.Operator);

        var response = await PatchStatusAsync(client, "999999", CancelBody);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ChangeStatus_CancellingAnAlreadyCancelledJob_Returns409WithDetail()
    {
        var (id, _) = await CreateReportedJobAsync();
        var client = await AuthTestHelper.CreateClientAsync(
            _factory, _fixture, "fake-operator", UserRole.Operator);

        var first = await PatchStatusAsync(client, id.ToString(), CancelBody);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PatchStatusAsync(client, id.ToString(), CancelBody);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Contains("from Cancelled to Cancelled",
            json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ChangeStatus_WithNonNumericJobId_Returns404()
    {
        var client = await AuthTestHelper.CreateClientAsync(
            _factory, _fixture, "fake-operator", UserRole.Operator);

        var response = await PatchStatusAsync(client, "abc", CancelBody);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}