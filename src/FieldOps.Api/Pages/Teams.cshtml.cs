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
    private readonly OperatorTeamService _teams;

    public TeamsModel(OperatorTeamService teams)
    {
        _teams = teams;
    }

    public IReadOnlyList<OperatorTeamItem> Teams { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Teams = await _teams.ListAsync(cancellationToken);
    }
}