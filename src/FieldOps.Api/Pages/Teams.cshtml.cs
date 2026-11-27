using System.Text.Json;
using FieldOps.Api.Assignment;
using FieldOps.Api.Domain;
using FieldOps.Api.Teams;
using FieldOps.Api.Time;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace FieldOps.Api.Pages;

[Authorize(
    AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme,
    Roles = nameof(UserRole.Operator) + "," + nameof(UserRole.Admin))]
public class TeamsModel : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly OperatorTeamService _teams;
    private readonly DemoLocationRefresher _refresher;
    private readonly IClock _clock;
    private readonly AssignmentSettings _settings;

    public TeamsModel(
        OperatorTeamService teams,
        DemoLocationRefresher refresher,
        IClock clock,
        IOptions<AssignmentSettings> settings)
    {
        _teams = teams;
        _refresher = refresher;
        _clock = clock;
        _settings = settings.Value;
    }

    public IReadOnlyList<OperatorTeamItem> Teams { get; private set; } = [];

    public string TeamsJson { get; private set; } = "[]";

    public DateTimeOffset Now { get; private set; }

    public int LocationMaxAgeSeconds => _settings.LocationMaxAgeSeconds;

    public bool ShowRefreshButton => _refresher.IsEnabled;

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Now = _clock.UtcNow;
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

    public async Task<IActionResult> OnPostRefreshLocationsAsync(CancellationToken cancellationToken)
    {
        if (!_refresher.IsEnabled)
        {
            return NotFound();
        }

        var count = await _refresher.RefreshAllAsync(cancellationToken);
        SuccessMessage = $"{count} ekibin konum zamanı yenilendi.";

        return RedirectToPage();
    }
}