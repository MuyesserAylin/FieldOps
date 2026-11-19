using FieldOps.Api.Assignment;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FieldOps.Api.Tests.Assignment;

public class AssignmentSettingsBindingTests
{
    private static AssignmentSettings Load(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.Configure<AssignmentSettings>(configuration.GetSection("Assignment"));

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<AssignmentSettings>>().Value;
    }

    [Fact]
    public void Values_from_configuration_are_bound()
    {
        var settings = Load(new Dictionary<string, string?>
        {
            ["Assignment:LocationMaxAgeSeconds"] = "30",
            ["Assignment:CityWidthKm"] = "20",
            ["Assignment:HighDistanceLimitPercent"] = "50",
            ["Assignment:MediumDistanceLimitPercent"] = "30",
            ["Assignment:LowDistanceLimitPercent"] = "10"
        });

        Assert.Equal(30, settings.LocationMaxAgeSeconds);
        Assert.Equal(20.0, settings.CityWidthKm);
        Assert.Equal(50.0, settings.HighDistanceLimitPercent);
        Assert.Equal(30.0, settings.MediumDistanceLimitPercent);
        Assert.Equal(10.0, settings.LowDistanceLimitPercent);
    }

    [Fact]
    public void Missing_section_leaves_zeros_and_validator_rejects_them()
    {
        var settings = Load(new Dictionary<string, string?>());

        Assert.Equal(0, settings.LocationMaxAgeSeconds);
        Assert.True(new AssignmentSettingsValidator().Validate(null, settings).Failed);
    }
}