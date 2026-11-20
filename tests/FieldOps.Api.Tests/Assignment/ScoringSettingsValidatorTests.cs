using FieldOps.Api.Assignment;
using Microsoft.Extensions.Options;

namespace FieldOps.Api.Tests.Assignment;

public class ScoringSettingsValidatorTests
{
    private static ValidateOptionsResult Validate(AssignmentSettings settings)
        => new AssignmentSettingsValidator().Validate(null, settings);

    [Fact]
    public void Weight_set_not_totaling_100_is_rejected()
    {
        var settings = TestSettings.Standard();
        settings.UrgentWeights.Distance = 50; // toplam 90 olur

        var result = Validate(settings);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("UrgentWeights"));
    }

    [Fact]
    public void Negative_weight_is_rejected_even_when_total_is_100()
    {
        var settings = TestSettings.Standard();
        settings.NormalWeights.Distance = -10;
        settings.NormalWeights.Response = 55; // toplam yine 100

        var result = Validate(settings);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("NormalWeights"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    [InlineData(1.1)]
    public void Capacity_ratio_must_be_above_0_and_at_most_1(double ratio)
    {
        var settings = TestSettings.Standard();
        settings.WorkloadCapacityRatio = ratio;

        Assert.True(Validate(settings).Failed);
    }

    [Fact]
    public void Capacity_ratio_of_exactly_1_is_accepted()
    {
        var settings = TestSettings.Standard();
        settings.WorkloadCapacityRatio = 1;

        Assert.True(Validate(settings).Succeeded);
    }

    [Fact]
    public void History_window_must_be_at_least_1()
    {
        var settings = TestSettings.Standard();
        settings.ResponseHistoryWindow = 0;
        settings.ResponseMinOffers = 0;

        Assert.True(Validate(settings).Failed);
    }

    [Fact]
    public void Min_offers_cannot_exceed_history_window()
    {
        var settings = TestSettings.Standard();
        settings.ResponseHistoryWindow = 10;
        settings.ResponseMinOffers = 11;

        Assert.True(Validate(settings).Failed);
    }

    [Fact]
    public void Negative_min_offers_is_rejected()
    {
        var settings = TestSettings.Standard();
        settings.ResponseMinOffers = -1;

        Assert.True(Validate(settings).Failed);
    }

    [Fact]
    public void City_height_far_from_service_area_is_rejected()
    {
        var settings = TestSettings.Standard();
        settings.CityHeightKm = 5;

        var result = Validate(settings);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("CityHeightKm"));
    }

    [Fact]
    public void City_height_close_to_service_area_is_accepted()
    {
        var settings = TestSettings.Standard();
        settings.CityHeightKm = 15.5;

        Assert.True(Validate(settings).Succeeded);
    }
}