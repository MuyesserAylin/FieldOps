using FieldOps.Api.Domain;

namespace FieldOps.Api.Assignment;
//Eleme fonksiyonuna verilen ekip "fotoğrafı"
//Verştabanı nesnesi değil,sadece veri taşır.
//DistanceKm: Ekibin işe kuşuçuşu uzaklığı ; çağıran hesaplar

public sealed record TeamSnapshot(
    int TeamId,
    TeamStatus Status,
    double DistanceKm,
    DateTimeOffset LocationUpdatedAt,
    DateTimeOffset? CooldownUntil,     //teklifi cevapsız bırakırsa 5 dk bekletmenin sonrası zamanı
    IReadOnlyCollection<int> SkillIds,
    IReadOnlyCollection<ShiftWindow> Shifts);