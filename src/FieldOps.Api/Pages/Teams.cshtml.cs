using System.Text.Json;
using FieldOps.Api.Domain;
using FieldOps.Api.Teams;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldOps.Api.Pages;

[Authorize(
    AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme,
    Roles = nameof(UserRole.Operator) + "," + nameof(UserRole.Admin))]
public class TeamsModel : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly OperatorTeamService _teams;

    public TeamsModel(OperatorTeamService teams)
    {
        _teams = teams;
    }

    public IReadOnlyList<OperatorTeamItem> Teams { get; private set; } = [];

    // Haritanın okuyacağı JSON. Sayfada data- özniteliğine konur (HTML-encode edilir).
    public string TeamsJson { get; private set; } = "[]";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Teams = await _teams.ListAsync(cancellationToken);

        TeamsJson = JsonSerializer.Serialize(
            Teams.Select(t => new
            {
                id = t.Id,
                name = t.Name,
                status = t.Status,
                statusLabel = TeamStatusLabels.Get(t.Status),
                lat = t.Latitude,
                lon = t.Longitude,
                onShift = t.OnShift,
                skills = t.Skills
                    .Select(s => $"{SkillLabels.Get(s.Name)} ({SkillLevelLabels.Get(s.Level)})")
                    .ToList()
            }),
            JsonOptions);
    }
}