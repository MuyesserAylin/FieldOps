using FieldOps.Api.Domain;
using FieldOps.Api.Jobs;
using FieldOps.Api.Reports;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldOps.Api.Pages;

[Authorize(
    AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme,
    Roles = nameof(UserRole.Operator) + "," + nameof(UserRole.Admin))]
public class OperatorModel : PageModel
{
    private readonly OperatorJobService _jobs;
    private readonly JobStatusService _statusService;

    public OperatorModel(OperatorJobService jobs, JobStatusService statusService)
    {
        _jobs = jobs;
        _statusService = statusService;
    }

    public string UserName => User.Identity?.Name ?? string.Empty;

    public string Role => User.FindFirst("role")?.Value ?? string.Empty;

    public IReadOnlyList<OperatorJobItem> Jobs { get; private set; } = [];

    public string? SelectedStatus { get; private set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(string? status, CancellationToken cancellationToken)
    {
        var parsed = ParseStatus(status);
        SelectedStatus = parsed?.ToString();
        Jobs = await _jobs.ListAsync(parsed, OperatorJobService.DefaultLimit, cancellationToken);
    }

    public async Task<IActionResult> OnPostCancelAsync(
        long id, string? status, CancellationToken cancellationToken)
    {
        var result = await _statusService.ChangeStatusAsync(
            id, JobStatus.Cancelled, cancellationToken);

        switch (result.Outcome)
        {
            case JobStatusChangeOutcome.Changed:
                SuccessMessage = $"İş #{id} iptal edildi.";
                break;
            case JobStatusChangeOutcome.NotFound:
                ErrorMessage = "İş bulunamadı.";
                break;
            case JobStatusChangeOutcome.InvalidTransition:
                ErrorMessage = $"Bu iş iptal edilemez (şu anki durum: " +
                    $"{StatusLabels.ForOperator(result.OldStatus!.Value.ToString())}).";
                break;
            case JobStatusChangeOutcome.ConcurrencyConflict:
                ErrorMessage = "İş başka biri tarafından değiştirildi. Listeyi yenileyip tekrar deneyin.";
                break;
        }

        return RedirectToPage(new { status });
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Login");
    }

    private static JobStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var name = Enum.GetNames<JobStatus>().FirstOrDefault(n =>
            string.Equals(n, status.Trim(), StringComparison.OrdinalIgnoreCase));

        return name is null ? null : Enum.Parse<JobStatus>(name);
    }
}