namespace FieldOps.Api.Reports;

public sealed record ValidatedReport(
    int FaultTypeId,
    double Latitude,
    double Longitude,
    string Description);

public static class CreateReportValidator
{
    public const int MaxDescriptionLength = 2000;

    public static (ValidatedReport? Report, Dictionary<string, string> Errors) Validate(
        CreateReportRequest request)
    {
        var errors = new Dictionary<string, string>();

        if (request.FaultTypeId is null)
        {
            errors["faultTypeId"] = "faultTypeId is required.";
        }

        if (request.Latitude is null)
        {
            errors["latitude"] = "latitude is required.";
        }
        else if (!(request.Latitude >= -90 && request.Latitude <= 90))
        {
            errors["latitude"] = "latitude must be between -90 and 90.";
        }

        if (request.Longitude is null)
        {
            errors["longitude"] = "longitude is required.";
        }
        else if (!(request.Longitude >= -180 && request.Longitude <= 180))
        {
            errors["longitude"] = "longitude must be between -180 and 180.";
        }

        var description = request.Description?.Trim();
        if (string.IsNullOrEmpty(description))
        {
            errors["description"] = "description is required.";
        }
        else if (description.Length > MaxDescriptionLength)
        {
            errors["description"] = $"description must be at most {MaxDescriptionLength} characters.";
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        var report = new ValidatedReport(
            request.FaultTypeId!.Value,
            request.Latitude!.Value,
            request.Longitude!.Value,
            description!);

        return (report, errors);
    }
}