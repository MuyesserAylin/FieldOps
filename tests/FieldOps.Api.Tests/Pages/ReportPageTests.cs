using System.Net;
using System.Text.RegularExpressions;
using FieldOps.Api.Tests.Database;
using FieldOps.Api.Tests.Reports;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Tests.Pages;

[Collection("Database")]
[Trait("Category", "Integration")]
public class ReportPageTests : IAsyncLifetime
{
    private readonly PostgisFixture _fixture;
    private readonly FieldOpsApiFactory _factory;

    public ReportPageTests(PostgisFixture fixture)
    {
        _fixture = fixture;
        _factory = new FieldOpsApiFactory(fixture.ConnectionString);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private HttpClient CreateClient() => _factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> GetFormTokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/Report");
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Form token was not found in the page.");
        return match.Groups[1].Value;
    }

    private static FormUrlEncodedContent Form(
        string? token, string faultTypeId, string latitude, string longitude, string description)
    {
        var fields = new Dictionary<string, string>
        {
            ["Input.FaultTypeId"] = faultTypeId,
            ["Input.Latitude"] = latitude,
            ["Input.Longitude"] = longitude,
            ["Input.Description"] = description
        };
        if (token is not null)
        {
            fields["__RequestVerificationToken"] = token;
        }

        return new FormUrlEncodedContent(fields);
    }

    // Razor, Türkçe karakterleri HTML kodu olarak yazabilir; karşılaştırmadan önce çözeriz.
    private static async Task<string> ReadHtmlAsync(HttpResponseMessage response)
        => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    [Fact]
    public async Task Get_ShowsFormWithTurkishFaultTypes()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/Report");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await ReadHtmlAsync(response);
        Assert.Contains("Arıza bildir", html);
        Assert.Contains("Doğalgaz kaçağı", html);
        Assert.Contains("Sokak lambası arızası", html);
    }

    [Fact]
    public async Task Get_ContainsMapWithServiceAreaSettings()
    {
        var client = CreateClient();

        var html = await ReadHtmlAsync(await client.GetAsync("/Report"));

        Assert.Contains("id=\"map\"", html);
        Assert.Contains("data-center-lat=\"39.7767\"", html);
        Assert.Contains("leaflet.js", html);
    }

    [Fact]
    public async Task Post_WithValidForm_RedirectsWithTrackingCodeAndSavesJob()
    {
        var client = CreateClient();
        var token = await GetFormTokenAsync(client);

        var response = await client.PostAsync(
            "/Report", Form(token, "1", "39.7767", "30.5206", "Smell of gas"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("created=", location);

        await using var db = _fixture.CreateDbContext();
        var job = await db.Jobs.SingleAsync();
        Assert.Equal(1, await db.JobStatusHistories.CountAsync());
        Assert.Equal(30.5206, job.Location.X, 6);
        Assert.Equal(39.7767, job.Location.Y, 6);

        var page = await client.GetAsync(location);
        var html = await ReadHtmlAsync(page);
        Assert.Contains(job.TrackingCode, html);
    }

    [Theory]
    [InlineData("1", "95", "30.5206", "Smell of gas", "Geçerli bir enlem")]
    [InlineData("1", "39.7767", "300", "Smell of gas", "Geçerli bir boylam")]
    [InlineData("1", "39.7767", "30.5206", "   ", "Açıklama boş olamaz")]
    [InlineData("", "39.7767", "30.5206", "Smell of gas", "Lütfen bir arıza türü seçin")]
    [InlineData("999", "39.7767", "30.5206", "Smell of gas", "Lütfen bir arıza türü seçin")]
    public async Task Post_WithInvalidForm_ShowsErrorAndSavesNothing(
        string faultTypeId, string latitude, string longitude, string description,
        string expectedMessage)
    {
        var client = CreateClient();
        var token = await GetFormTokenAsync(client);

        var response = await client.PostAsync(
            "/Report", Form(token, faultTypeId, latitude, longitude, description));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await ReadHtmlAsync(response);
        Assert.Contains(expectedMessage, html);

        await using var db = _fixture.CreateDbContext();
        Assert.Equal(0, await db.Jobs.CountAsync());
    }

    [Fact]
    public async Task Post_WithoutFormToken_Returns400AndSavesNothing()
    {
        var client = CreateClient();

        var response = await client.PostAsync(
            "/Report", Form(null, "1", "39.7767", "30.5206", "Smell of gas"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var db = _fixture.CreateDbContext();
        Assert.Equal(0, await db.Jobs.CountAsync());
    }
}