using FieldOps.Api.Domain;

namespace FieldOps.Api.Jobs;

public sealed record ChangeJobStatusRequest(string? Status);

public static class ChangeJobStatusValidator
{
    public static (JobStatus? Status, Dictionary<string, string> Errors) Validate(
        ChangeJobStatusRequest request)
    {
        var errors = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            errors["status"] = "status is required.";
            return (null, errors);
        }

        var name = Enum.GetNames<JobStatus>().FirstOrDefault(n =>
            string.Equals(n, request.Status.Trim(), StringComparison.OrdinalIgnoreCase));

        if (name is null)
        {
            errors["status"] =
                $"status must be one of: {string.Join(", ", Enum.GetNames<JobStatus>())}.";
            return (null, errors);
        }

        return (Enum.Parse<JobStatus>(name), errors);
    }
}