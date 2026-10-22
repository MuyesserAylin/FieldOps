using System;

namespace FieldOps.Api.Reports;

public sealed record ReportHistoryItem(
    string Status,
    DateTimeOffset ChangedAt);
public sealed record ReportStatusResponse
(
   string TrackingCode,
   string Status,
   string FaultType,
   string Description,
   DateTimeOffset CreatedAt,
   IReadOnlyList<ReportHistoryItem> History);
