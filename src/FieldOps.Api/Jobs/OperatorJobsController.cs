using FieldOps.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FieldOps.Api.Jobs;

[ApiController]
[Route("api/operator/jobs")]
[Authorize(Roles = nameof(UserRole.Operator) + "," + nameof(UserRole.Admin))]
public class OperatorJobsController : ControllerBase
{
    private readonly OperatorJobService _jobs;

    public OperatorJobsController(OperatorJobService jobs)
    {
        _jobs = jobs;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        string? status, int? limit, CancellationToken cancellationToken)
    {
        var errors = new ModelStateDictionary();

        JobStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            var name = Enum.GetNames<JobStatus>().FirstOrDefault(n =>
                string.Equals(n, status.Trim(), StringComparison.OrdinalIgnoreCase));

            if (name is null)
            {
                errors.AddModelError(
                    "status",
                    $"status must be one of: {string.Join(", ", Enum.GetNames<JobStatus>())}.");
            }
            else
            {
                parsedStatus = Enum.Parse<JobStatus>(name);
            }
        }

        var effectiveLimit = limit ?? OperatorJobService.DefaultLimit;
        if (effectiveLimit < 1)
        {
            errors.AddModelError("limit", "limit must be at least 1.");
        }

        if (!errors.IsValid)
        {
            return ValidationProblem(errors);
        }

        effectiveLimit = Math.Min(effectiveLimit, OperatorJobService.MaxLimit);

        var items = await _jobs.ListAsync(parsedStatus, effectiveLimit, cancellationToken);
        return Ok(items);
    }
}