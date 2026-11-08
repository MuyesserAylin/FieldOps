using FieldOps.Api.Domain;
using FieldOps.Api.Jobs;

namespace FieldOps.Api.Tests.Jobs;

public class OperatorStatusPolicyTests
{
    [Theory]
    [InlineData(JobStatus.Queued, false)]
    [InlineData(JobStatus.Offered, false)]
    [InlineData(JobStatus.OnRoute, false)]
    [InlineData(JobStatus.OnSite, false)]
    [InlineData(JobStatus.Completed, false)]
    [InlineData(JobStatus.Cancelled, true)]
    public void CanSet_OnlyCancelledIsAllowed(JobStatus status, bool expected)
    {
        Assert.Equal(expected, OperatorStatusPolicy.CanSet(status));
    }

    [Fact]
    public void Message_MentionsCancelled()
    {
        Assert.Contains("Cancelled", OperatorStatusPolicy.Message);
    }
} 