using System.Net;
using FieldOps.Api.Domain;
using FieldOps.Api.Tests.Auth;
using FieldOps.Api.Tests.Database;
using FieldOps.Api.Tests.Reports;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FieldOps.Api.Tests.Pages;

[Collection("Database")]
[Trait("Category", "Integration")]
public class LoginPageTests : IAsyncLifetime
{
    private readonly PostgisFixture _fixture;
    private readonly FieldOpsApiFactory _factory;

    public LoginPageTests(PostgisFixture fixture)
    {
        _fixture = fixture;
        _factory = new FieldOpsApiFactory(fixture.ConnectionString);
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private HttpClient CreateClient() => _factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client, string username, string password, bool withToken = true)
    {
        var fields = new Dictionary<string, string>
        {
            ["Input.Username"] = username,
            ["Input.Password"] = password
        };
        if (withToken)
        {
            fields["__RequestVerificationToken"] =
                await PageTestHelper.GetFormTokenAsync(client, "/Login");
        }

        return await client.PostAsync("/Login", new FormUrlEncodedContent(fields));
    }

    [Fact]
    public async Task Get_Login_ShowsForm()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await PageTestHelper.ReadHtmlAsync(response);
        Assert.Contains("Kullanıcı adı", html);
        Assert.Contains("Şifre", html);
    }

    [Fact]
    public async Task Get_Operator_WithoutLogin_RedirectsToLogin()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/Operator");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Login", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData(UserRole.Operator)]
    [InlineData(UserRole.Admin)]
    public async Task Login_WithOperatorOrAdmin_OpensOperatorPage(UserRole role)
    {
        await AuthTestHelper.AddUserAsync(_fixture, "panel.user", role);
        var client = CreateClient();

        var login = await PostLoginAsync(client, "panel.user", AuthTestHelper.FakePassword);

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/Operator", login.Headers.Location!.ToString());

        var page = await client.GetAsync("/Operator");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await PageTestHelper.ReadHtmlAsync(page);
        Assert.Contains("panel.user", html);
        Assert.Contains(role.ToString(), html);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShowsErrorAndStaysLoggedOut()
    {
        await AuthTestHelper.AddUserAsync(_fixture, "op1", UserRole.Operator);
        var client = CreateClient();

        var response = await PostLoginAsync(client, "op1", "wrong-password-123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Kullanıcı adı veya şifre hatalı", await PageTestHelper.ReadHtmlAsync(response));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Operator")).StatusCode);
    }

    [Fact]
    public async Task Login_WithInactiveUser_IsRefused()
    {
        await AuthTestHelper.AddUserAsync(_fixture, "op1", UserRole.Operator, isActive: false);
        var client = CreateClient();

        var response = await PostLoginAsync(client, "op1", AuthTestHelper.FakePassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Kullanıcı adı veya şifre hatalı", await PageTestHelper.ReadHtmlAsync(response));
    }

    [Fact]
    public async Task Login_WithTeamUser_IsRefusedAndStaysLoggedOut()
    {
        await AuthTestHelper.AddUserAsync(_fixture, "team1", UserRole.Team);
        var client = CreateClient();

        var response = await PostLoginAsync(client, "team1", AuthTestHelper.FakePassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("sadece operatör ve yönetici", await PageTestHelper.ReadHtmlAsync(response));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Operator")).StatusCode);
    }

    [Fact]
    public async Task Login_WithoutFormToken_Returns400()
    {
        await AuthTestHelper.AddUserAsync(_fixture, "op1", UserRole.Operator);
        var client = CreateClient();

        var response = await PostLoginAsync(
            client, "op1", AuthTestHelper.FakePassword, withToken: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        await AuthTestHelper.AddUserAsync(_fixture, "op1", UserRole.Operator);
        var client = CreateClient();
        await PostLoginAsync(client, "op1", AuthTestHelper.FakePassword);
        var token = await PageTestHelper.GetFormTokenAsync(client, "/Operator");

        var logout = await client.PostAsync(
            "/Operator?handler=Logout",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Contains("/Login", logout.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/Operator")).StatusCode);
    }
}