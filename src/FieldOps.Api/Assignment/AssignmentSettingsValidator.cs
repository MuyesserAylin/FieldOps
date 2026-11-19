using Microsoft.Extensions.Options;

namespace FieldOps.Api.Assignment;

// Uygulama açılırken Assignment ayarlarını kontrol eder; saçma bir değer varsa açılmaz.
public sealed class AssignmentSettingsValidator : IValidateOptions<AssignmentSettings>
{
    private const double MaxCityWidthDifferenceKm = 1.0;

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

        CheckPercent(errors, nameof(options.HighDistanceLimitPercent), options.HighDistanceLimitPercent);
        CheckPercent(errors, nameof(options.MediumDistanceLimitPercent), options.MediumDistanceLimitPercent);
        CheckPercent(errors, nameof(options.LowDistanceLimitPercent), options.LowDistanceLimitPercent);

        // Daha acil iş daha geniş alandan ekip arayabilmeli.
        if (options.HighDistanceLimitPercent < options.MediumDistanceLimitPercent
            || options.MediumDistanceLimitPercent < options.LowDistanceLimitPercent)
        {
            errors.Add("Mesafe yüzdeleri High >= Medium >= Low sırasında olmalı.");
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
}