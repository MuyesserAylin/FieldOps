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
    private readonly JobStatusService _statusService;

    public OperatorJobsController(OperatorJobService jobs, JobStatusService statusService)
    {
        _jobs = jobs;
        _statusService = statusService;
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


    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> ChangeStatus(
    long id, ChangeJobStatusRequest request, CancellationToken cancellationToken)
    {
        var (newStatus, errors) = ChangeJobStatusValidator.Validate(request);

        var modelState = new ModelStateDictionary();
        foreach (var (field, message) in errors)
        {
            modelState.AddModelError(field, message);
        }

        if (newStatus is not null && !OperatorStatusPolicy.CanSet(newStatus.Value))
        {
            modelState.AddModelError("status", OperatorStatusPolicy.Message);
        }

        if (!modelState.IsValid)
        {
            return ValidationProblem(modelState);
        }

        var result = await _statusService.ChangeStatusAsync(
            id, newStatus!.Value, cancellationToken);

        return result.Outcome switch
        {
            JobStatusChangeOutcome.Changed => Ok(new ChangeJobStatusResponse(
                id,
                result.OldStatus!.Value.ToString(),
                result.NewStatus!.Value.ToString())),

            JobStatusChangeOutcome.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Job not found."),

            JobStatusChangeOutcome.InvalidTransition => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Job status cannot be changed.",
                detail: $"Job status cannot change from {result.OldStatus} to {result.NewStatus}."),

            JobStatusChangeOutcome.ConcurrencyConflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Job status cannot be changed.",
                detail: "The job was changed by someone else. Reload and try again."),

            _ => throw new InvalidOperationException(
                $"Unexpected outcome: {result.Outcome}")
        };
    }
}
public sealed record ChangeJobStatusResponse(long Id, string OldStatus, string NewStatus);