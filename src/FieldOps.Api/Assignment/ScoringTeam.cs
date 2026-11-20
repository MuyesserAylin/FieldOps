using FieldOps.Api.Domain;

namespace FieldOps.Api.Assignment;

// Puanlamaya giren, eleme sonrası kalan bir ekibin "fotoğrafı".
// Puanlama saf fonksiyondur: ihtiyacı olan her şeyi buradan alır, veritabanına bakmaz.
// WorkedMinutes: ekibin bu vardiyada üstlendiği (biten + devam eden) işlerin tahmini toplam süresi (dk).
// ShiftMinutes: ekibin vardiya süresi (dk). Kapasite buradan hesaplanır.
// SkillLevel: ekibin İŞİN İSTEDİĞİ becerideki seviyesi (çağıran kod bulup verir).
// OffersConsidered: son ResponseHistoryWindow teklif içinden değerlendirmeye alınan teklif sayısı.
// OffersAccepted: bunlardan kaçı kabul edildi (ret ve zaman aşımı kabul sayılmaz).

public sealed record ScoringTeam(
    int TeamId,
    double DistanceKm,
    double WorkedMinutes,
    double ShiftMinutes,
    TeamSkillLevel SkillLevel,
    int OffersConsidered,
    int OffersAccepted);
