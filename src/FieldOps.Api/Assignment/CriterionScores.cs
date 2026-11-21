using FieldOps.Api.Domain;

namespace FieldOps.Api.Assignment;

// Hepsi saf fonksiyondur: sadece aldığı sayılara bakar, veritabanına, saate, ayarlara dokunmaz.
// Notlar yuvarlanmaz; yuvarlama sadece ekranda gösterirken yapılır.
public static class CriterionScores
{
    // Mesafe notu = 100 x max(0, 1 - d / D)
    // d: ekibin işe kuşuçuşu uzaklığı (km). D: referans mesafe (km), GetDistanceReferenceKm verir.
    // Neden: yakın ekip daha iyi. d = 0 ise 100, d = D ise 0, arası orantılı. max(0, ...) notun eksiye düşmesini engeller.
    public static double Distance(double distanceKm, double referenceKm)
    {
        if (!double.IsFinite(distanceKm) || distanceKm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceKm), distanceKm, "Uzaklık 0 veya daha büyük, sonlu bir sayı olmalı.");
        }

        if (!double.IsFinite(referenceKm) || referenceKm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(referenceKm), referenceKm, "Referans mesafe 0'dan büyük, sonlu bir sayı olmalı.");
        }

        return 100 * Math.Max(0, 1 - distanceKm / referenceKm);
    }



    // İşYükü notu = 100 x max(0, 1 - T / K)
    // T: ekibin bu vardiyada üstlendiği (biten + devam eden) işlerin tahmini toplam süresi (dk).
    // K: vardiyanın kullanılabilir kapasitesi = capacityRatio x vardiya süresi (dk).
    // Neden: az çalışmış ekip daha yüksek not alır, iş adil dağılır. T >= K ise not 0 ("tamamen dolu"), eksiye düşmez.
    public static double Workload(double workedMinutes, double shiftMinutes, double capacityRatio)
    {
        if (!double.IsFinite(workedMinutes) || workedMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workedMinutes), workedMinutes, "Çalışılan süre 0 veya daha büyük, sonlu bir sayı olmalı.");
        }

        if (!double.IsFinite(shiftMinutes) || shiftMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shiftMinutes), shiftMinutes, "Vardiya süresi 0'dan büyük, sonlu bir sayı olmalı.");
        }

        if (!double.IsFinite(capacityRatio) || capacityRatio <= 0 || capacityRatio > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityRatio), capacityRatio, "Kapasite oranı 0'dan büyük, en fazla 1 olmalı.");
        }

        var capacityMinutes = capacityRatio * shiftMinutes;
        return 100 * Math.Max(0, 1 - workedMinutes / capacityMinutes);
    }



    // Beceri notu = 100 x seviye / 3
    // Seviye: 1 (temel) -> 33,33; 2 (yeterli) -> 66,67; 3 (deneyimli) -> 100.
    public static double Skill(TeamSkillLevel level)
    {
        if (!Enum.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Bilinmeyen beceri seviyesi.");
        }

        return 100 * (int)level / 3.0;
    }




    // GeçmişCevap notu = 100 x kabul / teklif
    // offersConsidered: değerlendirmeye alınan teklif sayısı (son ResponseHistoryWindow teklif içinden).
    // offersAccepted: bunlardan kabul edilenler. Ret ve zaman aşımı "kabul edilmedi" sayılır.
    // Teklif sayısı minOffers'tan azsa (ya da hiç yoksa) oran güvenilir değildir: tam not (100) verilir, yeni ekip haksız cezalanmaz.
    public static double Response(int offersConsidered, int offersAccepted, int minOffers)
    {
        if (offersConsidered < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offersConsidered), offersConsidered, "Teklif sayısı 0 veya daha büyük olmalı.");
        }

        if (offersAccepted < 0 || offersAccepted > offersConsidered)
        {
            throw new ArgumentOutOfRangeException(nameof(offersAccepted), offersAccepted, "Kabul sayısı 0 ile teklif sayısı arasında olmalı.");
        }

        if (minOffers < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minOffers), minOffers, "En az teklif sayısı 0 veya daha büyük olmalı.");
        }

        if (offersConsidered == 0 || offersConsidered < minOffers)
        {
            return 100;
        }

        return 100.0 * offersAccepted / offersConsidered;
    }
}