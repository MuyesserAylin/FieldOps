namespace FieldOps.Api.Teams;

// Ekibin konum bilgisinin "yaşı" (şimdi - konum zamanı) ve ekranda gösterimi.
// "Eski" kuralı eleme ile aynıdır (ana belge 7.1, kural 4): yaş LocationMaxAgeSeconds'tan BÜYÜKSE eskidir
// (tam sınırda taze); gelecekteki bir konum zamanı eski sayılmaz.
public static class LocationAge
{
    public static bool IsStale(TimeSpan age, int maxAgeSeconds) => age.TotalSeconds > maxAgeSeconds;

    public static string Format(TimeSpan age)
    {
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age.TotalMinutes < 1)
        {
            return $"{(int)age.TotalSeconds} sn";
        }

        if (age.TotalHours < 1)
        {
            return $"{(int)age.TotalMinutes} dk";
        }

        if (age.TotalDays < 1)
        {
            var hours = (int)age.TotalHours;
            var minutes = age.Minutes;
            return minutes == 0 ? $"{hours} sa" : $"{hours} sa {minutes} dk";
        }

        return $"{(int)age.TotalDays} gün";
    }
}