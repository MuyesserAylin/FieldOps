using FieldOps.Api.Domain;

namespace FieldOps.Api.Tests.Assignment;

public class ScoringSettingsTests
{
    [Fact]
    public void Standard_weight_sets_total_100()
    {
        var settings = TestSettings.Standard();

        Assert.Equal(100.0, settings.UrgentWeights.Total, 6);
        Assert.Equal(100.0, settings.NormalWeights.Total, 6);
    }

    [Theory]
    [InlineData(Urgency.Critical)]
    [InlineData(Urgency.High)]
    public void Critical_and_high_use_urgent_weights(Urgency urgency)
    {
        var settings = TestSettings.Standard();

        Assert.Same(settings.UrgentWeights, settings.GetWeights(urgency));
    }

    [Theory]
    [InlineData(Urgency.Medium)]
    [InlineData(Urgency.Low)]
    public void Medium_and_low_use_normal_weights(Urgency urgency)
    {
        var settings = TestSettings.Standard();

        Assert.Same(settings.NormalWeights, settings.GetWeights(urgency));
    }

    [Fact]
    public void GetWeights_unknown_urgency_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TestSettings.Standard().GetWeights((Urgency)99));
    }

    [Fact]
    public void Critical_reference_is_the_city_diagonal_25_km()
    {
        Assert.Equal(25.0, TestSettings.Standard().GetDistanceReferenceKm(Urgency.Critical), 3);
    }

    [Fact]
    public void Other_references_equal_the_elimination_limits()
    {
        var settings = TestSettings.Standard();

        Assert.Equal(12.0, settings.GetDistanceReferenceKm(Urgency.High), 6);
        Assert.Equal(8.0, settings.GetDistanceReferenceKm(Urgency.Medium), 6);
        Assert.Equal(4.0, settings.GetDistanceReferenceKm(Urgency.Low), 6);
    }

    [Fact]
    public void Critical_reference_follows_city_size()
    {
        var settings = TestSettings.Standard();
        settings.CityWidthKm = 30;
        settings.CityHeightKm = 40;

        Assert.Equal(50.0, settings.GetDistanceReferenceKm(Urgency.Critical), 6);
    }

    [Fact]
    public void GetDistanceReferenceKm_unknown_urgency_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TestSettings.Standard().GetDistanceReferenceKm((Urgency)99));
    }

    [Fact]
    public void Standard_city_height_matches_service_area_within_1_km()
    {
        var difference = Math.Abs(TestSettings.Standard().CityHeightKm - ServiceArea.HeightKm);

        Assert.True(difference <= 1.0, $"Fark {difference:F2} km.");
    }
}