using FieldOps.Api.Reports;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldOps.Api.Pages;

public class TrackModel : PageModel
{
    private readonly ReportService _reports;

    public TrackModel(ReportService reports)
    {
        _reports = reports;
    }

    public string? Code { get; private set; }

    public ReportStatusResponse? Report { get; private set; }

    public bool IsNotFound { get; private set; }

    public async Task OnGetAsync(string? code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        Code = code.Trim();

        // ReportsController'daki Get metodunda yaptığımızla aynı çağrı:
        var normalized = TrackingCodeNormalizer.Normalize(Code);
        Report = await _reports.GetStatusAsync(normalized, cancellationToken);
        IsNotFound = Report is null;
    }
}