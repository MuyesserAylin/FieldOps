using System;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FieldOps.Api.Reports;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly ReportService _service;

    public ReportsController(ReportService reportService)
    {
        _service = reportService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateReportRequest request, CancellationToken cancellationToken)
    {
        var (report, errors) = CreateReportValidator.Validate(request);

        if (report is null)
        {
            return ValidationProblem(ToModelState(errors));
        }

        var response = await _service.CreateAsync(report, cancellationToken);
        if (response is null)
        {
            var notFound = new Dictionary<string, string>
            {
                ["faultTypeId"] = "faultTypeId does not exist."
            };
            return ValidationProblem(ToModelState(notFound));
        }

        return Created($"/api/reports/{response.TrackingCode}", response);
    }

    [HttpGet("{trackingCode}")]
    public async Task<IActionResult> GetStatus(
        string trackingCode, CancellationToken cancellationToken
    )
    {
        var response = await _service.GetStatusAsync(trackingCode, cancellationToken);
        if (response is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Report not found");
        }
        return Ok(response);
    }
    private static ModelStateDictionary ToModelState(Dictionary<string, string> errors)
    {
        var modelState = new ModelStateDictionary();
        foreach (var (field, message) in errors)
        {
            modelState.AddModelError(field, message);
        }
        return modelState;
    }
}
