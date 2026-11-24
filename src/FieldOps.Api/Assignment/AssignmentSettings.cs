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

    // Şehrin kuzey-güney yüksekliği (km). Kritik işte mesafe notunun ölçüsü olan köşegeni bulmak için lazım.
    // Hizmet alanından (ServiceArea) en fazla 1 km farklı olabilir. Başlangıç değeri: 15
    public double CityHeightKm { get; set; }

    // İşYükü notunda kapasite K = bu oran x vardiya süresi (dk). Başlangıç değeri: 0,75
    public double WorkloadCapacityRatio { get; set; }

    // GeçmişCevap notunda son kaç teklife bakılır. Başlangıç değeri: 10
    public int ResponseHistoryWindow { get; set; }

    // Bundan az teklif almış ekibe tam not (100) verilir; yeni ekip haksız cezalanmasın. Başlangıç değeri: 5
    public int ResponseMinOffers { get; set; }


    // Kuyrukta bekleyen işin aciliyeti her bu kadar dakikada bir basamak yükselir (en fazla "yüksek").
    // Amaç: düşük öncelikli iş sonsuza kadar aç kalmasın. Başlangıç değeri: 30
    public int UrgencyEscalationMinutes { get; set; }
    // Ağırlık setleri. Sayılar appsettings.json'dan gelir.
    // Başlangıç değerleri: acil 60/5/10/25, normal 35/40/15/10
    public ScoringWeights UrgentWeights { get; set; } = new();
    public ScoringWeights NormalWeights { get; set; } = new();

    // Bu aciliyet için hangi ağırlık seti kullanılır? Kritik ve yüksek: acil set; orta ve düşük: normal set.
    public ScoringWeights GetWeights(Urgency urgency) => urgency switch
    {
        Urgency.Critical or Urgency.High => UrgentWeights,
        Urgency.Medium or Urgency.Low => NormalWeights,
        _ => throw new ArgumentOutOfRangeException(nameof(urgency), urgency, "Bilinmeyen aciliyet.")
    };

    // Mesafe notundaki D (km). Kritikte eleme sınırı yok, o yüzden şehrin köşegeni;
    // diğerlerinde D eleme sınırı. Hiçbir mesafe D'den büyük olamaz, not eksiye düşmez.
    public double GetDistanceReferenceKm(Urgency urgency) => urgency switch
    {
        Urgency.Critical => Math.Sqrt(CityWidthKm * CityWidthKm + CityHeightKm * CityHeightKm),
        Urgency.High or Urgency.Medium or Urgency.Low => GetDistanceLimitKm(urgency)!.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(urgency), urgency, "Bilinmeyen aciliyet.")
    };

    // Mesafe sınırı yüzdeleri. Kritikte sınır yoktur.
    public double HighDistanceLimitPercent { get; set; }    // başlangıç %60
    public double MediumDistanceLimitPercent { get; set; }  // başlangıç %40
    public double LowDistanceLimitPercent { get; set; }     // başlangıç %20

    // Bu aciliyette aday olabilecek en uzak ekibin uzaklığı (km). 
    //Bu ekibi aday yapalım mı
    public double? GetDistanceLimitKm(Urgency urgency) => urgency switch
    {
        Urgency.Critical => null,
        Urgency.High => CityWidthKm * HighDistanceLimitPercent / 100,
        Urgency.Medium => CityWidthKm * MediumDistanceLimitPercent / 100,
        Urgency.Low => CityWidthKm * LowDistanceLimitPercent / 100,
        _ => throw new ArgumentOutOfRangeException(nameof(urgency), urgency, "Bilinmeyen aciliyet.")
    };
}