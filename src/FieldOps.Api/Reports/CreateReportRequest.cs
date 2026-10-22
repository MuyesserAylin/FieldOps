namespace FieldOps.Api.Reports;

public sealed record CreateReportRequest(
    int? FaultTypeId,
    double? Latitude,
    double? Longitude,
    string? Description);