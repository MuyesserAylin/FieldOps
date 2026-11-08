using System.Net;
using System.Text.Json;

namespace FieldOps.Api.Tests;

public static class ProblemAssert
{
    public static async Task HasFieldErrorAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = json.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name);
        Assert.Contains(names, n => string.Equals(n, field, StringComparison.OrdinalIgnoreCase));
    }
}