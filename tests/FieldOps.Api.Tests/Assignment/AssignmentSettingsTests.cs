using FieldOps.Api.Domain;

namespace FieldOps.Api.Tests.Assignment;

public class AssignmentSettingsTests
{
    [Fact]
    public void Critical_has_no_distance_limit()
    {
        Assert.Null(TestSettings.Standard().GetDistanceLimitKm(Urgency.Critical));
    }

    [Fact]
    public void Standard_limits_are_12_8_4_km()
    {
        var settings = TestSettings.Standard();

        Assert.Equal(12.0, settings.GetDistanceLimitKm(Urgency.High)!.Value, 6);
        Assert.Equal(8.0, settings.GetDistanceLimitKm(Urgency.Medium)!.Value, 6);
        Assert.Equal(4.0, settings.GetDistanceLimitKm(Urgency.Low)!.Value, 6);
    }

    [Fact]
    public void Limits_scale_with_city_width()
    {
        var settings = TestSettings.Standard();
        settings.CityWidthKm = 10;

        Assert.Equal(6.0, settings.GetDistanceLimitKm(Urgency.High)!.Value, 6);
        Assert.Equal(4.0, settings.GetDistanceLimitKm(Urgency.Medium)!.Value, 6);
        Assert.Equal(2.0, settings.GetDistanceLimitKm(Urgency.Low)!.Value, 6);
    }

    [Fact]
    public void Limits_follow_percent_settings()
    {
        var settings = TestSettings.Standard();
        settings.LowDistanceLimitPercent = 50;

        Assert.Equal(10.0, settings.GetDistanceLimitKm(Urgency.Low)!.Value, 6);
    }

    [Fact]
    public void Unknown_urgency_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TestSettings.Standard().GetDistanceLimitKm((Urgency)99));
    }
}