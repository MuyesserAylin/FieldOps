using System.Net;
using System.Text.RegularExpressions;
using FieldOps.Api.Domain;
using FieldOps.Api.Tests.Auth;
using FieldOps.Api.Tests.Database;
using FieldOps.Api.Tests.Reports;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FieldOps.Api.Tests.Pages;

internal static class PageTestHelper
{
    public static async Task<string> GetFormTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Form token was not found in the page.");
        return match.Groups[1].Value;
    }

    public static async Task<string> ReadHtmlAsync(HttpResponseMessage response)
        => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    public static async Task<HttpClient> CreateLoggedInClientAsync(
        FieldOpsApiFactory factory, PostgisFixture fixture, string username, UserRole role)
    {
        await AuthTestHelper.AddUserAsync(fixture, username, role);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await GetFormTokenAsync(client, "/Login");

        var response = await client.PostAsync("/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Input.Username"] = username,
                ["Input.Password"] = AuthTestHelper.FakePassword,
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }
}