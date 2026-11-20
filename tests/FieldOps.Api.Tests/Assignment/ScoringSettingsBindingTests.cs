using FieldOps.Api.Assignment;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FieldOps.Api.Tests.Assignment;

public class ScoringSettingsBindingTests
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
    public void Scoring_values_from_configuration_are_bound()
    {
        var settings = Load(new Dictionary<string, string?>
        {
            ["Assignment:CityHeightKm"] = "15",
            ["Assignment:WorkloadCapacityRatio"] = "0.8",
            ["Assignment:ResponseHistoryWindow"] = "12",
            ["Assignment:ResponseMinOffers"] = "4",
            ["Assignment:UrgentWeights:Distance"] = "70",
            ["Assignment:UrgentWeights:Workload"] = "5",
            ["Assignment:UrgentWeights:Skill"] = "5",
            ["Assignment:UrgentWeights:Response"] = "20",
            ["Assignment:NormalWeights:Distance"] = "30",
            ["Assignment:NormalWeights:Workload"] = "40",
            ["Assignment:NormalWeights:Skill"] = "20",
            ["Assignment:NormalWeights:Response"] = "10"
        });

        Assert.Equal(15.0, settings.CityHeightKm);
        Assert.Equal(0.8, settings.WorkloadCapacityRatio);
        Assert.Equal(12, settings.ResponseHistoryWindow);
        Assert.Equal(4, settings.ResponseMinOffers);
        Assert.Equal(70.0, settings.UrgentWeights.Distance);
        Assert.Equal(20.0, settings.UrgentWeights.Response);
        Assert.Equal(100.0, settings.UrgentWeights.Total, 6);
        Assert.Equal(30.0, settings.NormalWeights.Distance);
        Assert.Equal(20.0, settings.NormalWeights.Skill);
        Assert.Equal(100.0, settings.NormalWeights.Total, 6);
    }
}