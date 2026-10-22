using System;

namespace FieldOps.Api.Reports;

public sealed record CreateReportResponse
(
    string TrackingCode,
    string Status,
    string FaultType,
    DateTimeOffset CreatedAt
);
