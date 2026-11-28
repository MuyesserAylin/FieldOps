using System.Net;
using FieldOps.Api.Domain;
using FieldOps.Api.Tests.Database;
using FieldOps.Api.Tests.Reports;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests.Pages;

[Collection("Database")]
[Trait("Category", "Integration")]
public class TeamsLocationPageTests : IAsyncLifetime
{
    private const int GasSkillId = 1;
    private const string RefreshButtonText = "Konumları yenile (demo)";
    private const string RefreshUrl = "/Teams?handler=RefreshLocations";

    private readonly PostgisFixture _fixture;
    private readonly FieldOpsApiFactory _factory;
    private readonly FieldOpsApiFactory _demoFactory;

    public TeamsLocationPageTests(PostgisFixture fixture)
    {
        _fixture = fixture;
        _factory = new FieldOpsApiFactory(fixture.ConnectionString);
        _demoFactory = new FieldOpsApiFactory(fixture.ConnectionString, demoLocationRefresh: true);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _demoFactory.DisposeAsync();
    }

    private async Task<int> AddTeamAsync(string name, TimeSpan locationAge)
    {
        var team = new Team(
            name,
            new Point(30.5206, 39.7767) { SRID = 4326 },
            DateTimeOffset.UtcNow - locationAge);
        team.AddSkill(GasSkillId, TeamSkillLevel.Expert);

        await using var db = _fixture.CreateDbContext();
        db.Teams.Add(team);
        await db.SaveChangesAsync();
        return team.Id;
    }

    private async Task<DateTimeOffset> ReadLocationTimeAsync(int teamId)
    {
        await using var db = _fixture.CreateDbContext();
        var team = await db.Teams.AsNoTracking().SingleAsync(t => t.Id == teamId);
        return team.LocationUpdatedAt;
    }

    private static FormUrlEncodedContent FormWith(string token) =>
        new(new Dictionary<string, string> { ["__RequestVerificationToken"] = token });


    [Fact]
    public async Task Get_ShowsLocationAgeAndTheStaleWarning()
    {
        await AddTeamAsync("Old Team", TimeSpan.FromHours(2));
        await AddTeamAsync("Fresh Team", TimeSpan.Zero);
        var client = await PageTestHelper.CreateLoggedInClientAsync(_factory, _fixture, "op1", UserRole.Operator);

        var html = await PageTestHelper.ReadHtmlAsync(await client.GetAsync("/Teams"));

        Assert.Contains("Konum yaşı", html);
        Assert.Contains("2 sa", html);
        Assert.Contains("<span class=\"stale\">Eski</span>", html);
        Assert.Contains("1 ekibin konumu", html);
        Assert.Contains("saniyeden eski", html);
    }

    [Fact]
    public async Task Get_WhenEveryLocationIsFresh_ShowsNoStaleWarning()
    {
        await AddTeamAsync("Fresh One", TimeSpan.Zero);
        await AddTeamAsync("Fresh Two", TimeSpan.FromSeconds(5));
        var client = await PageTestHelper.CreateLoggedInClientAsync(_factory, _fixture, "op1", UserRole.Operator);

        var html = await PageTestHelper.ReadHtmlAsync(await client.GetAsync("/Teams"));

        Assert.Contains("Konum yaşı", html);
        Assert.DoesNotContain("<span class=\"stale\">", html);
        Assert.DoesNotContain("saniyeden eski", html);
    }


    [Fact]
    public async Task Get_WhenDemoModeIsOff_HidesTheRefreshButton()
    {
        await AddTeamAsync("Some Team", TimeSpan.Zero);
        var client = await PageTestHelper.CreateLoggedInClientAsync(_factory, _fixture, "op1", UserRole.Operator);

        var html = await PageTestHelper.ReadHtmlAsync(await client.GetAsync("/Teams"));

        Assert.DoesNotContain(RefreshButtonText, html);
    }

    [Fact]
    public async Task Get_InDemoMode_ShowsTheRefreshButton()
    {
        await AddTeamAsync("Some Team", TimeSpan.Zero);
        var client = await PageTestHelper.CreateLoggedInClientAsync(_demoFactory, _fixture, "op1", UserRole.Operator);

        var html = await PageTestHelper.ReadHtmlAsync(await client.GetAsync("/Teams"));

        Assert.Contains(RefreshButtonText, html);
    }


    [Fact]
    public async Task PostRefresh_WhenDemoModeIsOff_ReturnsNotFound_AndChangesNothing()
    {
        var teamId = await AddTeamAsync("Old Team", TimeSpan.FromHours(2));
        var before = await ReadLocationTimeAsync(teamId);
        var client = await PageTestHelper.CreateLoggedInClientAsync(_factory, _fixture, "op1", UserRole.Operator);

        var token = await PageTestHelper.GetFormTokenAsync(client, "/Operator");
        var response = await client.PostAsync(RefreshUrl, FormWith(token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await ReadLocationTimeAsync(teamId));
    }

    [Fact]
    public async Task PostRefresh_InDemoMode_RefreshesLocations_RedirectsAndShowsTheMessage()
    {
        var teamId = await AddTeamAsync("Old Team", TimeSpan.FromHours(2));
        var before = await ReadLocationTimeAsync(teamId);
        var client = await PageTestHelper.CreateLoggedInClientAsync(_demoFactory, _fixture, "op1", UserRole.Operator);

        var token = await PageTestHelper.GetFormTokenAsync(client, "/Teams");
        var response = await client.PostAsync(RefreshUrl, FormWith(token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/Teams", response.Headers.Location!.ToString());

        var after = await ReadLocationTimeAsync(teamId);
        Assert.True(after > before);
        Assert.True(DateTimeOffset.UtcNow - after < TimeSpan.FromMinutes(1), "Konum zamanı 'şimdi' olmalı.");

        // Yönlendirilen sayfada mesaj görünür ve ekip artık taze
        var html = await PageTestHelper.ReadHtmlAsync(await client.GetAsync("/Teams"));
        Assert.Contains("1 ekibin konum zamanı yenilendi.", html);
        Assert.DoesNotContain("<span class=\"stale\">", html);
    }

    [Fact]
    public async Task PostRefresh_WithoutLogin_RedirectsToLogin()
    {
        var client = _demoFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync(
            RefreshUrl, new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Login", response.Headers.Location!.ToString());
    }
}