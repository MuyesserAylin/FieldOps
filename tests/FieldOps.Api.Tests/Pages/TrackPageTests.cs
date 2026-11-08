using System.Net;
using System.Net.Http.Json;
using FieldOps.Api.Reports;
using FieldOps.Api.Tests.Database;
using FieldOps.Api.Tests.Reports;

namespace FieldOps.Api.Tests.Pages;

[Collection("Database")]
[Trait("Category", "Integration")]
public class TrackPageTests : IAsyncLifetime
{
    private readonly PostgisFixture _fixture;
    private readonly FieldOpsApiFactory _factory;

    public TrackPageTests(PostgisFixture fixture)
    {
        _fixture = fixture;
        _factory = new FieldOpsApiFactory(fixture.ConnectionString);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private static async Task<string> ReadHtmlAsync(HttpResponseMessage response)
        => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    private async Task<string> CreateReportAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/reports", new
        {
            faultTypeId = 1,
            latitude = 39.7767,
            longitude = 30.5206,
            description = "Smell of gas"
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CreateReportResponse>();
        return body!.TrackingCode;
    }

    [Fact]
    public async Task Get_WithoutCode_ShowsEmptyForm()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Track");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await ReadHtmlAsync(response);
        Assert.Contains("Takip kodu", html);
        Assert.DoesNotContain("bulunamadı", html);
    }

    [Fact]
    public async Task Get_WithKnownCode_ShowsStatusAndHistory()
    {
        var client = _factory.CreateClient();
        var code = await CreateReportAsync(client);

        var response = await client.GetAsync($"/Track?code={code}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await ReadHtmlAsync(response);
        Assert.Contains("Bildirim alındı, ekip bekleniyor", html);
        Assert.Contains("Doğalgaz kaçağı", html);
        Assert.Contains("Smell of gas", html);
        Assert.Contains(code, html);
    }

    [Fact]
    public async Task Get_WithLowercaseCode_StillFindsReport()
    {
        var client = _factory.CreateClient();
        var code = await CreateReportAsync(client);

        var response = await client.GetAsync($"/Track?code={code.ToLowerInvariant()}");

        var html = await ReadHtmlAsync(response);
        Assert.DoesNotContain("bulunamadı", html);
        Assert.Contains("Doğalgaz kaçağı", html);
    }

    [Fact]
    public async Task Get_WithUnknownCode_ShowsNotFoundMessage()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Track?code=ZZZZZZZZZZZZ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await ReadHtmlAsync(response);
        Assert.Contains("Bu kodla bir bildirim bulunamadı", html);
    }
}