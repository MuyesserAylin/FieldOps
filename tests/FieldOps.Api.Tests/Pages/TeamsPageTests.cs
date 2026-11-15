using System.Net;
using FieldOps.Api.Domain;
using FieldOps.Api.Tests.Database;
using FieldOps.Api.Tests.Reports;
using Microsoft.AspNetCore.Mvc.Testing;
using NetTopologySuite.Geometries;

namespace FieldOps.Api.Tests.Pages;

[Collection("Database")]
[Trait("Category", "Integration")]
public class TeamsPageTests : IAsyncLifetime
{
    private const int GasSkillId = 1;

    private readonly PostgisFixture _fixture;
    private readonly FieldOpsApiFactory _factory;

    public TeamsPageTests(PostgisFixture fixture)
    {
        _fixture = fixture;
        _factory = new FieldOpsApiFactory(fixture.ConnectionString);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task AddTeamAsync(string name)
    {
        var team = new Team(
            name,
            new Point(30.5206, 39.7767) { SRID = 4326 },
            DateTimeOffset.UtcNow);
        team.AddSkill(GasSkillId, TeamSkillLevel.Expert);

        await using var db = _fixture.CreateDbContext();
        db.Teams.Add(team);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Get_WithoutLogin_RedirectsToLogin()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Teams");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Get_AsOperator_ShowsTeamNames()
    {
        await AddTeamAsync("Test Team One");
        await AddTeamAsync("Test Team Two");
        var client = await PageTestHelper.CreateLoggedInClientAsync(
            _factory, _fixture, "op1", UserRole.Operator);

        var response = await client.GetAsync("/Teams");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await PageTestHelper.ReadHtmlAsync(response);
        Assert.Contains("Test Team One", html);
        Assert.Contains("Test Team Two", html);
    }

    [Fact]
    public async Task Get_WithNoTeams_ShowsEmptyMessage()
    {
        var client = await PageTestHelper.CreateLoggedInClientAsync(
            _factory, _fixture, "op1", UserRole.Operator);

        var html = await PageTestHelper.ReadHtmlAsync(await client.GetAsync("/Teams"));

        Assert.Contains("Henüz ekip yok.", html);
    }

    [Fact]
    public async Task Get_TeamNameWithHtml_IsEncoded()
    {
        await AddTeamAsync("<b>Bold</b>");
        var client = await PageTestHelper.CreateLoggedInClientAsync(
            _factory, _fixture, "op1", UserRole.Operator);

        // Ham metin: ReadHtmlAsync kullanılmaz, çünkü o HTML-decode eder.
        var raw = await (await client.GetAsync("/Teams")).Content.ReadAsStringAsync();

        Assert.Contains("&lt;b&gt;Bold", raw);
        Assert.DoesNotContain("<b>Bold", raw);
    }
}