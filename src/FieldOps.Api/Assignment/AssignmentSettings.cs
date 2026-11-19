using FieldOps.Api.Domain;

namespace FieldOps.Api.Assignment;

// Eleme ve (sonra) puanlama için ayarlanabilir sayılar.
// Değerler appsettings.json içindeki "Assignment" bölümünden gelir; sınıfta varsayılan değer yoktur.
public sealed class AssignmentSettings
{
    // Konum bilgisi bu süreden eskiyse ekip aday olamaz (saniye). Başlangıç değeri: 60
    public int LocationMaxAgeSeconds { get; set; }

    // Şehrin doğu-batı genişliği (km). Mesafe sınırları bunun yüzdesidir.
    // Hizmet alanından (ServiceArea) en fazla 1 km farklı olabilir. Başlangıç değeri: 20
    public double CityWidthKm { get; set; }

    // Mesafe sınırı yüzdeleri. Kritikte sınır yoktur.
    public double HighDistanceLimitPercent { get; set; }    // başlangıç %60
    public double MediumDistanceLimitPercent { get; set; }  // başlangıç %40
    public double LowDistanceLimitPercent { get; set; }     // başlangıç %20

    // Bu aciliyette aday olabilecek en uzak ekibin uzaklığı (km). 
    public double? GetDistanceLimitKm(Urgency urgency) => urgency switch
    {
        Urgency.Critical => null,
        Urgency.High => CityWidthKm * HighDistanceLimitPercent / 100,
        Urgency.Medium => CityWidthKm * MediumDistanceLimitPercent / 100,
        Urgency.Low => CityWidthKm * LowDistanceLimitPercent / 100,
        _ => throw new ArgumentOutOfRangeException(nameof(urgency), urgency, "Bilinmeyen aciliyet.")
    };
}