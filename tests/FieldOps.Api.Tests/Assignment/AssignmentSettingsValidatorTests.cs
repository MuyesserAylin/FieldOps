using FieldOps.Api.Assignment;
using Microsoft.Extensions.Options;

namespace FieldOps.Api.Tests.Assignment;

public class AssignmentSettingsValidatorTests
{
    private static ValidateOptionsResult Validate(AssignmentSettings settings)
        => new AssignmentSettingsValidator().Validate(null, settings);

    [Fact]
    public void Standard_settings_are_valid()
    {
        Assert.True(Validate(TestSettings.Standard()).Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Location_max_age_must_be_positive(int seconds)
    {
        var settings = TestSettings.Standard();
        settings.LocationMaxAgeSeconds = seconds;

        Assert.True(Validate(settings).Failed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Percent_must_be_between_0_exclusive_and_100(double percent)
    {
        var settings = TestSettings.Standard();
        settings.HighDistanceLimitPercent = percent;

        Assert.True(Validate(settings).Failed);
    }

    [Fact]
    public void Percents_must_not_decrease_with_urgency()
    {
        var settings = TestSettings.Standard();
        settings.HighDistanceLimitPercent = 30;

        Assert.True(Validate(settings).Failed);
    }

    [Fact]
    public void City_width_far_from_service_area_is_rejected()
    {
        var settings = TestSettings.Standard();
        settings.CityWidthKm = 10;

        var result = Validate(settings);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("CityWidthKm"));
    }

    [Fact]
    public void City_width_close_to_service_area_is_accepted()
    {
        var settings = TestSettings.Standard();
        settings.CityWidthKm = 19.5;

        Assert.True(Validate(settings).Succeeded);
    }
}