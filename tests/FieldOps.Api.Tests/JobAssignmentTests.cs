using FieldOps.Api.Domain;

namespace FieldOps.Api.Tests;

public class JobAssignmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewJob_HasNoAssignment()
    {
        var job = JobTestHelper.CreateQueuedJob();

        Assert.Null(job.AssignedTeamId);
        Assert.Null(job.AssignedAt);
    }

    [Theory]
    [InlineData(JobStatus.OnRoute)]
    [InlineData(JobStatus.OnSite)]
    public void AssignTo_OnRouteOrOnSiteJob_SetsBothFields(JobStatus status)
    {
        var job = JobTestHelper.CreateJobInStatus(status);

        job.AssignTo(7, Now);

        Assert.Equal(7, job.AssignedTeamId);
        Assert.Equal(Now, job.AssignedAt);
    }

    [Theory]
    [InlineData(JobStatus.Queued)]
    [InlineData(JobStatus.Offered)]
    [InlineData(JobStatus.Completed)]
    [InlineData(JobStatus.Cancelled)]
    public void AssignTo_JobInOtherStatus_ThrowsAndLeavesFieldsEmpty(JobStatus status)
    {
        var job = JobTestHelper.CreateJobInStatus(status);

        Assert.Throws<InvalidOperationException>(() => job.AssignTo(7, Now));

        Assert.Null(job.AssignedTeamId);
        Assert.Null(job.AssignedAt);
    }

    [Fact]
    public void AssignTo_SecondTime_ThrowsAndKeepsFirstAssignment()
    {
        var job = JobTestHelper.CreateJobInStatus(JobStatus.OnRoute);
        job.AssignTo(7, Now);

        Assert.Throws<InvalidOperationException>(() => job.AssignTo(8, Now.AddMinutes(1)));

        Assert.Equal(7, job.AssignedTeamId);
        Assert.Equal(Now, job.AssignedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AssignTo_NonPositiveTeamId_Throws(int teamId)
    {
        var job = JobTestHelper.CreateJobInStatus(JobStatus.OnRoute);

        Assert.Throws<ArgumentOutOfRangeException>(() => job.AssignTo(teamId, Now));

        Assert.Null(job.AssignedTeamId);
    }

    [Fact]
    public void ClearAssignment_AssignedJob_ClearsBothFields()
    {
        var job = JobTestHelper.CreateJobInStatus(JobStatus.OnRoute);
        job.AssignTo(7, Now);

        job.ClearAssignment();

        Assert.Null(job.AssignedTeamId);
        Assert.Null(job.AssignedAt);
    }

    [Fact]
    public void ClearAssignment_UnassignedJob_DoesNothing()
    {
        var job = JobTestHelper.CreateQueuedJob();

        job.ClearAssignment();

        Assert.Null(job.AssignedTeamId);
        Assert.Null(job.AssignedAt);
    }

    [Fact]
    public void ClearAssignment_CompletedJob_ThrowsAndKeepsAssignment()
    {
        var job = JobTestHelper.CreateJobInStatus(JobStatus.OnSite);
        job.AssignTo(7, Now);
        job.TransitionTo(JobStatus.Completed, Now.AddMinutes(30));

        Assert.Throws<InvalidOperationException>(() => job.ClearAssignment());

        Assert.Equal(7, job.AssignedTeamId);
        Assert.Equal(Now, job.AssignedAt);
    }
}