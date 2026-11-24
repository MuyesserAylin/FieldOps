using FieldOps.Api.Assignment;
using FieldOps.Api.Domain;

namespace FieldOps.Api.Tests.Assignment;

public class UrgencyEscalatorTests
{
    private static readonly DateTimeOffset QueuedSince = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static Urgency Escalate(Urgency initial, int waitedSeconds, AssignmentSettings? settings = null)
        => UrgencyEscalator.GetEffectiveUrgency(
            initial, QueuedSince, QueuedSince.AddSeconds(waitedSeconds), settings ?? TestSettings.Standard());

    [Theory]
    // Düşük
    [InlineData(Urgency.Low, 0, Urgency.Low)]
    [InlineData(Urgency.Low, 1799, Urgency.Low)]
    [InlineData(Urgency.Low, 1800, Urgency.Medium)]
    [InlineData(Urgency.Low, 3599, Urgency.Medium)]
    [InlineData(Urgency.Low, 3600, Urgency.High)]
    [InlineData(Urgency.Low, 36000, Urgency.High)]
    // Orta
    [InlineData(Urgency.Medium, 1799, Urgency.Medium)]
    [InlineData(Urgency.Medium, 1800, Urgency.High)]
    [InlineData(Urgency.Medium, 5400, Urgency.High)]
    // Yüksek ve kritik: hiç değişmez
    [InlineData(Urgency.High, 0, Urgency.High)]
    [InlineData(Urgency.High, 1800, Urgency.High)]
    [InlineData(Urgency.High, 360000, Urgency.High)]
    [InlineData(Urgency.Critical, 0, Urgency.Critical)]
    [InlineData(Urgency.Critical, 1800, Urgency.Critical)]
    [InlineData(Urgency.Critical, 360000, Urgency.Critical)]
    [InlineData(Urgency.Low, -600, Urgency.Low)]
    [InlineData(Urgency.Medium, -36000, Urgency.Medium)]
    public void Escalates_one_step_per_30_minutes_up_to_High(Urgency initial, int waitedSeconds, Urgency expected)
    {
        Assert.Equal(expected, Escalate(initial, waitedSeconds));
    }

    [Theory]
    [InlineData(600, Urgency.Medium)]  // 10 dk -> 1 dilim
    [InlineData(1200, Urgency.High)]   // 20 dk -> 2 dilim
    [InlineData(1199, Urgency.Medium)] // 19 dk 59 sn
    public void Uses_the_configured_minutes(int waitedSeconds, Urgency expected)
    {
        var settings = TestSettings.Standard();
        settings.UrgencyEscalationMinutes = 10;

        Assert.Equal(expected, Escalate(Urgency.Low, waitedSeconds, settings));
    }

    [Fact]
    public void Same_input_always_gives_the_same_answer()
    {
        var first = Escalate(Urgency.Low, 1800);
        var second = Escalate(Urgency.Low, 1800);

        Assert.Equal(Urgency.Medium, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Extremely_long_wait_does_not_overflow()
    {
        var result = UrgencyEscalator.GetEffectiveUrgency(
            Urgency.Low, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, TestSettings.Standard());

        Assert.Equal(Urgency.High, result);
    }

    [Fact]
    public void Undefined_urgency_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Escalate((Urgency)99, 1800));
    }

    [Fact]
    public void Null_settings_throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => UrgencyEscalator.GetEffectiveUrgency(Urgency.Low, QueuedSince, QueuedSince.AddHours(1), null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_escalation_minutes_throws(int minutes)
    {
        var settings = TestSettings.Standard();
        settings.UrgencyEscalationMinutes = minutes;

        Assert.Throws<ArgumentOutOfRangeException>(() => Escalate(Urgency.Low, 1800, settings));
    }
}