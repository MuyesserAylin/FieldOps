using FieldOps.Api.Domain;
using FieldOps.Api.Jobs;

namespace FieldOps.Api.Tests.Jobs;

public class ChangeJobStatusValidatorTests
{
    [Theory]
    [InlineData("Queued", JobStatus.Queued)]
    [InlineData("Offered", JobStatus.Offered)]
    [InlineData("OnRoute", JobStatus.OnRoute)]
    [InlineData("OnSite", JobStatus.OnSite)]
    [InlineData("Completed", JobStatus.Completed)]
    [InlineData("Cancelled", JobStatus.Cancelled)]
    public void Validate_EveryStatusName_IsAccepted(string text, JobStatus expected)
    {
        var (status, errors) = ChangeJobStatusValidator.Validate(new ChangeJobStatusRequest(text));

        Assert.Empty(errors);
        Assert.Equal((JobStatus?)expected, status);
    }

    [Fact]
    public void Validate_IgnoresCaseAndSurroundingSpaces()
    {
        var (status, errors) = ChangeJobStatusValidator.Validate(
            new ChangeJobStatusRequest("  oFFered  "));

        Assert.Empty(errors);
        Assert.Equal((JobStatus?)JobStatus.Offered, status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Unknown")]
    [InlineData("1")]
    [InlineData("EnRoute")]
    public void Validate_InvalidStatus_ReturnsStatusError(string? text)
    {
        var (status, errors) = ChangeJobStatusValidator.Validate(new ChangeJobStatusRequest(text));

        Assert.Null(status);
        Assert.Contains("status", errors.Keys);
    }
}