using FieldOps.Api.Data;
using FieldOps.Api.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Pages;

public sealed class ReportInput
{
    public int? FaultTypeId { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Description { get; set; }
}

public sealed record FaultTypeOption(int Id, string Label);

public class ReportModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ReportService _reports;

    public ReportModel(AppDbContext db, ReportService reports)
    {
        _db = db;
        _reports = reports;
    }

    [BindProperty]
    public ReportInput Input { get; set; } = new();

    public IReadOnlyList<FaultTypeOption> FaultTypes { get; private set; } = [];

    public Dictionary<string, string> Errors { get; } = new();

    public string? CreatedCode { get; private set; }

    public async Task OnGetAsync(string? created, CancellationToken cancellationToken)
    {
        CreatedCode = created;
        await LoadFaultTypesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var (report, validationErrors) = CreateReportValidator.Validate(
            new CreateReportRequest(
                Input.FaultTypeId, Input.Latitude, Input.Longitude, Input.Description));

        if (report is not null)
        {
            var response = await _reports.CreateAsync(report, cancellationToken);
            if (response is not null)
            {
                return RedirectToPage(new { created = response.TrackingCode });
            }

            validationErrors["faultTypeId"] = "faultTypeId does not exist.";
        }

        foreach (var field in validationErrors.Keys)
        {
            Errors[field] = ErrorText(field);
        }

        await LoadFaultTypesAsync(cancellationToken);
        return Page();
    }

    private async Task LoadFaultTypesAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.FaultTypes
            .AsNoTracking()
            .OrderBy(f => f.Id)
            .Select(f => new { f.Id, f.Name })
            .ToListAsync(cancellationToken);

        FaultTypes = rows
            .Select(r => new FaultTypeOption(r.Id, FaultTypeLabels.For(r.Name)))
            .ToList();
    }

    private static string ErrorText(string field) => field switch
    {
        "faultTypeId" => "Lütfen bir arıza türü seçin.",
        "latitude" => "Geçerli bir enlem girin (-90 ile 90 arası).",
        "longitude" => "Geçerli bir boylam girin (-180 ile 180 arası).",
        "description" => "Açıklama boş olamaz ve en fazla 2000 karakter olmalı.",
        _ => "Geçersiz değer."
    };
}