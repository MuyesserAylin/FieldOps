using FieldOps.Api.Domain;

namespace FieldOps.Api.Tests;

public class JobStatusTransitionTests
{
    private static readonly (JobStatus From, JobStatus To)[] AllowedPairs =
    [
        (JobStatus.Queued, JobStatus.Offered),
        (JobStatus.Queued, JobStatus.Cancelled),
        (JobStatus.Offered, JobStatus.OnRoute),
        (JobStatus.Offered, JobStatus.Queued),
        (JobStatus.Offered, JobStatus.Cancelled),
        (JobStatus.OnRoute, JobStatus.OnSite),
        (JobStatus.OnRoute, JobStatus.Queued),
        (JobStatus.OnRoute, JobStatus.Cancelled),
        (JobStatus.OnSite, JobStatus.Completed),
        (JobStatus.OnSite, JobStatus.Queued),
        (JobStatus.OnSite, JobStatus.Cancelled),
    ];

    public static TheoryData<JobStatus, JobStatus> AllowedTransitions
    {
        get
        {
            var data = new TheoryData<JobStatus, JobStatus>();
            foreach (var (from, to) in AllowedPairs)
            {
                data.Add(from, to);
            }

            return data;
        }
    }

    public static TheoryData<JobStatus, JobStatus> ForbiddenTransitions
    {
        get
        {
            var data = new TheoryData<JobStatus, JobStatus>();
            foreach (var from in Enum.GetValues<JobStatus>())
            {
                foreach (var to in Enum.GetValues<JobStatus>())
                {
                    if (!AllowedPairs.Contains((from, to)))
                    {
                        data.Add(from, to);
                    }
                }
            }

            return data;
        }
    }
    public static TheoryData<JobStatus, JobStatus> FinalStatusTransitions
    {
        get
        {
            var data = new TheoryData<JobStatus, JobStatus>();
            JobStatus[] finalStatuses = [JobStatus.Completed, JobStatus.Cancelled];

            foreach (var from in finalStatuses)
            {
                foreach (var to in Enum.GetValues<JobStatus>())
                {
                    data.Add(from, to);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void TransitionTo_AllowedTransition_UpdatesStatusAndReturnsHistory(
        JobStatus from, JobStatus to)
    {
        var job = JobTestHelper.CreateJobInStatus(from);
        var changedAt = new DateTimeOffset(2026, 1, 1, 13, 0, 0, TimeSpan.Zero);

        var history = job.TransitionTo(to, changedAt);

        Assert.Equal(to, job.Status);
        Assert.Equal(from, history.OldStatus);
        Assert.Equal(to, history.NewStatus);
        Assert.Equal(changedAt, history.ChangedAt);
        Assert.Same(job, history.Job);
    }

    [Fact]
    public void AllowedPlusForbidden_CoversAll36Pairs()
    {
        var total = Enum.GetValues<JobStatus>().Length;

        Assert.Equal(6, total);
        Assert.Equal(11, AllowedTransitions.Count());
        Assert.Equal(25, ForbiddenTransitions.Count());
        Assert.Equal(total * total, AllowedTransitions.Count() + ForbiddenTransitions.Count());
    }

    [Theory]
    [MemberData(nameof(ForbiddenTransitions))]
    public void TransitionTo_ForbiddenTransition_ThrowsAndKeepsStatus(
        JobStatus from, JobStatus to)
    {
        var job = JobTestHelper.CreateJobInStatus(from);

        Assert.Throws<InvalidJobTransitionException>(() =>
            job.TransitionTo(to, DateTimeOffset.UnixEpoch));

        Assert.Equal(from, job.Status);
    }

    [Theory]
    [MemberData(nameof(FinalStatusTransitions))]
    public void TransitionTo_FromFinalStatus_AlwaysThrows(JobStatus from, JobStatus to)
    {
        var job = JobTestHelper.CreateJobInStatus(from);

        Assert.Throws<InvalidJobTransitionException>(() =>
            job.TransitionTo(to, DateTimeOffset.UnixEpoch));

        Assert.Equal(from, job.Status);
    }

    [Fact]
    public void TransitionTo_RejectionLoop_RecordsEveryStepInOrder()
    {
        var job = JobTestHelper.CreateQueuedJob();
        var start = new DateTimeOffset(2026, 1, 1, 13, 0, 0, TimeSpan.Zero);
        JobStatus[] path =
        [
            JobStatus.Offered,
            JobStatus.Queued,
            JobStatus.Offered,
            JobStatus.OnRoute,
            JobStatus.OnSite,
            JobStatus.Completed
        ];

        var histories = new List<JobStatusHistory>();
        foreach (var step in path)
        {
            histories.Add(job.TransitionTo(step, start.AddMinutes(histories.Count)));
        }

        Assert.Equal(6, histories.Count);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal((JobStatus?)JobStatus.Queued, histories[0].OldStatus);

        for (var i = 1; i < histories.Count; i++)
        {
            Assert.Equal((JobStatus?)histories[i - 1].NewStatus, histories[i].OldStatus);
        }
    }

    [Fact]
    public void TransitionTo_ForbiddenTransition_ExceptionCarriesFromToAndMessage()
    {
        var job = JobTestHelper.CreateJobInStatus(JobStatus.Queued);

        var exception = Assert.Throws<InvalidJobTransitionException>(() =>
            job.TransitionTo(JobStatus.Completed, DateTimeOffset.UnixEpoch));

        Assert.Equal(JobStatus.Queued, exception.From);
        Assert.Equal(JobStatus.Completed, exception.To);
        Assert.Contains("Queued", exception.Message);
        Assert.Contains("Completed", exception.Message);
    }
}