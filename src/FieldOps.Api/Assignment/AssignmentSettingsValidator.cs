using Microsoft.Extensions.Options;

namespace FieldOps.Api.Assignment;

public sealed class AssignmentSettingsValidator : IValidateOptions<AssignmentSettings>
{
    private const double MaxCityWidthDifferenceKm = 1.0;
    private const double MaxCityHeightDifferenceKm = 1.0;
    private const double WeightTotalTolerance = 0.0001;

    public ValidateOptionsResult Validate(string? name, AssignmentSettings options)
    {
        var errors = new List<string>();

        if (options.LocationMaxAgeSeconds <= 0)
        {
            errors.Add("Assignment:LocationMaxAgeSeconds 0'dan büyük olmalı.");
        }

        if (options.CityWidthKm <= 0)
        {
            errors.Add("Assignment:CityWidthKm 0'dan büyük olmalı.");
        }
        else
        {
            var mapWidthKm = ServiceArea.WidthKm;
            if (Math.Abs(options.CityWidthKm - mapWidthKm) > MaxCityWidthDifferenceKm)
            {
                errors.Add(
                    $"Assignment:CityWidthKm ({options.CityWidthKm} km), hizmet alanı genişliğinden " +
                    $"({mapWidthKm:F2} km) en fazla {MaxCityWidthDifferenceKm} km farklı olabilir.");
            }
        }

        if (options.CityHeightKm <= 0)
        {
            errors.Add("Assignment:CityHeightKm 0'dan büyük olmalı.");
        }
        else
        {
            var mapHeightKm = ServiceArea.HeightKm;
            if (Math.Abs(options.CityHeightKm - mapHeightKm) > MaxCityHeightDifferenceKm)
            {
                errors.Add(
                    $"Assignment:CityHeightKm ({options.CityHeightKm} km), hizmet alanı yüksekliğinden " +
                    $"({mapHeightKm:F2} km) en fazla {MaxCityHeightDifferenceKm} km farklı olabilir.");
            }
        }

        CheckPercent(errors, nameof(options.HighDistanceLimitPercent), options.HighDistanceLimitPercent);
        CheckPercent(errors, nameof(options.MediumDistanceLimitPercent), options.MediumDistanceLimitPercent);
        CheckPercent(errors, nameof(options.LowDistanceLimitPercent), options.LowDistanceLimitPercent);

        if (options.HighDistanceLimitPercent < options.MediumDistanceLimitPercent
            || options.MediumDistanceLimitPercent < options.LowDistanceLimitPercent)
        {
            errors.Add("Mesafe yüzdeleri High >= Medium >= Low sırasında olmalı.");
        }

        CheckWeights(errors, "UrgentWeights", options.UrgentWeights);
        CheckWeights(errors, "NormalWeights", options.NormalWeights);

        if (options.WorkloadCapacityRatio <= 0 || options.WorkloadCapacityRatio > 1)
        {
            errors.Add("Assignment:WorkloadCapacityRatio 0'dan büyük, en fazla 1 olmalı.");
        }

        if (options.ResponseHistoryWindow < 1)
        {
            errors.Add("Assignment:ResponseHistoryWindow en az 1 olmalı.");
        }

        if (options.ResponseMinOffers < 0)
        {
            errors.Add("Assignment:ResponseMinOffers 0'dan küçük olamaz.");
        }
        else if (options.ResponseMinOffers > options.ResponseHistoryWindow)
        {
            errors.Add("Assignment:ResponseMinOffers, ResponseHistoryWindow'dan büyük olamaz.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private static void CheckPercent(List<string> errors, string name, double value)
    {
        if (value <= 0 || value > 100)
        {
            errors.Add($"Assignment:{name} 0 ile 100 arasında olmalı (0 hariç).");
        }
    }

    private static void CheckWeights(List<string> errors, string setName, ScoringWeights weights)
    {
        if (weights.Distance < 0 || weights.Workload < 0 || weights.Skill < 0 || weights.Response < 0)
        {
            errors.Add($"Assignment:{setName} içindeki ağırlıklar 0'dan küçük olamaz.");
        }

        if (Math.Abs(weights.Total - 100) > WeightTotalTolerance)
        {
            errors.Add($"Assignment:{setName} ağırlıklarının toplamı 100 olmalı (şu an {weights.Total}).");
        }
    }
}