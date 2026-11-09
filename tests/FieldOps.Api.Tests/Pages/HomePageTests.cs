using System.Net;
using FieldOps.Api.Tests.Database;
using FieldOps.Api.Tests.Reports;

namespace FieldOps.Api.Tests.Pages;

[Collection("Database")]
[Trait("Category", "Integration")]
public class HomePageTests : IAsyncLifetime
{
    private readonly PostgisFixture _fixture;
    private readonly FieldOpsApiFactory _factory;

    public HomePageTests(PostgisFixture fixture)
    {
        _fixture = fixture;
        _factory = new FieldOpsApiFactory(fixture.ConnectionString);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task HomePage_Returns200HtmlWithoutLogin()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("FieldOps", await response.Content.ReadAsStringAsync());
    }
}