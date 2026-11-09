using System.Net;
using System.Net.Http.Json;
using FieldOps.Api.Domain;
using FieldOps.Api.Reports;
using FieldOps.Api.Tests.Database;
using FieldOps.Api.Tests.Reports;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Tests.Pages;

[Collection("Database")]
[Trait("Category", "Integration")]
public class OperatorPageTests : IAsyncLifetime
{
    private readonly PostgisFixture _fixture;
    private readonly FieldOpsApiFactory _factory;

    public OperatorPageTests(PostgisFixture fixture)
    {
        _fixture = fixture;
        _factory = new FieldOpsApiFactory(fixture.ConnectionString);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<string> CreateReportAsync(int faultTypeId, string description)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/reports", new
        {
            faultTypeId,
            latitude = 39.7767,
            longitude = 30.5206,
            description
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CreateReportResponse>();
        return body!.TrackingCode;
    }

    private async Task<long> GetJobIdAsync(string trackingCode)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Jobs
            .Where(j => j.TrackingCode == trackingCode)
            .Select(j => j.Id)
            .SingleAsync();
    }

    private static async Task<HttpResponseMessage> PostCancelAsync(HttpClient client, long id)
    {
        var token = await PageTestHelper.GetFormTokenAsync(client, "/Operator");
        return await client.PostAsync(
            "/Operator?handler=Cancel",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = id.ToString(),
                ["status"] = "",
                ["__RequestVerificationToken"] = token
            }));
    }

    [Fact]
    public async Task Get_WithNoJobs_ShowsEmptyMessage()
    {
        var client = await PageTestHelper.CreateLoggedInClientAsync(
            _factory, _fixture, "op1", UserRole.Operator);

        var html = await PageTestHelper.ReadHtmlAsync(await client.GetAsync("/Operator"));

        Assert.Contains("Henüz iş yok", html);
    }

    [Fact]
    public async Task Get_ListsJobsMostUrgentFirst()
    {
        var potholeCode = await CreateReportAsync(4, "Hole in the road");
        var gasCode = await CreateReportAsync(1, "Smell of gas");
        var client = await PageTestHelper.CreateLoggedInClientAsync(
            _factory, _fixture, "op1", UserRole.Operator);

        var response = await client.GetAsync("/Operator");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await PageTestHelper.ReadHtmlAsync(response);
        Assert.Contains("Doğalgaz kaçağı", html);
        Assert.Contains("Yol çukuru", html);
        Assert.Contains("Kritik", html);
        Assert.True(
            html.IndexOf(gasCode, StringComparison.Ordinal)
                < html.IndexOf(potholeCode, StringComparison.Ordinal),
            "The critical job should be listed before the medium one.");
    }

    [Fact]
    public async Task Get_WithStatusFilter_ShowsOnlyMatchingJobs()
    {
        var code = await CreateReportAsync(1, "Smell of gas");
        var client = await PageTestHelper.CreateLoggedInClientAsync(
            _factory, _fixture, "op1", UserRole.Operator);

        var queued = await PageTestHelper.ReadHtmlAsync(
            await client.GetAsync("/Operator?status=Queued"));
        var cancelled = await PageTestHelper.ReadHtmlAsync(
            await client.GetAsync("/Operator?status=Cancelled"));

        Assert.Contains(code, queued);
        Assert.DoesNotContain(code, cancelled);
        Assert.Contains("Henüz iş yok", cancelled);
    }

    [Fact]
    public async Task Cancel_ChangesStatusAndCitizenSeesIt()
    {
        var code = await CreateReportAsync(1, "Smell of gas");
        var id = await GetJobIdAsync(code);
        var client = await PageTestHelper.CreateLoggedInClientAsync(
            _factory, _fixture, "op1", UserRole.Operator);

        var response = await PostCancelAsync(client, id);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using (var db = _fixture.CreateDbContext())
        {
            var job = await db.Jobs.SingleAsync(j => j.Id == id);
            Assert.Equal(JobStatus.Cancelled, job.Status);
            Assert.Equal(2, await db.JobStatusHistories.CountAsync(h => h.JobId == id));
        }

        var operatorHtml = await PageTestHelper.ReadHtmlAsync(await client.GetAsync("/Operator"));
        Assert.Contains($"İş #{id} iptal edildi", operatorHtml);

        var citizenHtml = await PageTestHelper.ReadHtmlAsync(
            await _factory.CreateClient().GetAsync($"/Track?code={code}"));
        Assert.Contains("İptal edildi", citizenHtml);
    }

    [Fact]
    public async Task Cancel_Twice_ShowsErrorTheSecondTime()
    {
        var code = await CreateReportAsync(1, "Smell of gas");
        var id = await GetJobIdAsync(code);
        var client = await PageTestHelper.CreateLoggedInClientAsync(
            _factory, _fixture, "op1", UserRole.Operator);

        await PostCancelAsync(client, id);
        var second = await PostCancelAsync(client, id);

        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        var html = await PageTestHelper.ReadHtmlAsync(await client.GetAsync("/Operator"));
        Assert.Contains("Bu iş iptal edilemez", html);

        await using var db = _fixture.CreateDbContext();
        Assert.Equal(2, await db.JobStatusHistories.CountAsync(h => h.JobId == id));
    }

    [Fact]
    public async Task Cancel_WithUnknownId_ShowsNotFoundMessage()
    {
        var client = await PageTestHelper.CreateLoggedInClientAsync(
            _factory, _fixture, "op1", UserRole.Operator);

        var response = await PostCancelAsync(client, 999999);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var html = await PageTestHelper.ReadHtmlAsync(await client.GetAsync("/Operator"));
        Assert.Contains("İş bulunamadı", html);
    }

    [Fact]
    public async Task Cancel_WithoutLogin_RedirectsToLoginAndChangesNothing()
    {
        var code = await CreateReportAsync(1, "Smell of gas");
        var id = await GetJobIdAsync(code);
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync(
            "/Operator?handler=Cancel",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = id.ToString() }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Login", response.Headers.Location!.ToString());

        await using var db = _fixture.CreateDbContext();
        Assert.Equal(JobStatus.Queued, (await db.Jobs.SingleAsync(j => j.Id == id)).Status);
    }
}