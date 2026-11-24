using FieldOps.Api.Domain;

namespace FieldOps.Api.Assignment;

// Kuyrukta bekleyen işin güncel aciliyetini hesaplar (ana belge bölüm 6: bekleme yükseltmesi).
// Kural: iş kuyrukta her UrgencyEscalationMinutes dakikada bir basamak yükselir; tavan "yüksek"tir.
// Başlangıçta yüksek ya da kritik olan iş hiç değişmez. "Kritik" yalnızca arıza türünden ya da operatörden gelir;
// beklemiş bir sokak lambası gaz kaçağıyla aynı seviyeye çıkmamalı.
// Amaç: düşük öncelikli iş yoğun günde sonsuza kadar aç kalmasın.
// Her zaman BAŞLANGIÇ aciliyetinden hesaplanır; böylece aynı girdi aynı sonucu verir ve
// fonksiyonu tekrar tekrar çağırmak zararsızdır (iş her çağrıda bir basamak birden yükselmez).
// Saf fonksiyon: veritabanına ve saate bakmaz, "şimdi" dışarıdan gelir.
public static class UrgencyEscalator
{
    public static Urgency GetEffectiveUrgency(
        Urgency initialUrgency,
        DateTimeOffset queuedSince,
        DateTimeOffset now,
        AssignmentSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!Enum.IsDefined(initialUrgency))
        {
            throw new ArgumentOutOfRangeException(nameof(initialUrgency), initialUrgency, "Bilinmeyen aciliyet.");
        }

        if (settings.UrgencyEscalationMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings), settings.UrgencyEscalationMinutes, "UrgencyEscalationMinutes 0'dan büyük olmalı.");
        }

        // Yüksek ve kritik işler bekleyerek değişmez.
        if (initialUrgency >= Urgency.High)
        {
            return initialUrgency;
        }

        // Saat kayması gibi nedenlerle bekleme negatif çıkarsa yükseltme yapılmaz.
        var waited = now - queuedSince;
        if (waited <= TimeSpan.Zero)
        {
            return initialUrgency;
        }

        // Sadece dolmuş dilimler sayılır. Çok uzun beklemede sayı taşmasın diye önce tavandaki basamak sayısıyla sınırlıyoruz.
        var completedSteps = Math.Floor(waited.TotalMinutes / settings.UrgencyEscalationMinutes);
        var stepsToCeiling = (int)Urgency.High - (int)initialUrgency;
        var steps = (int)Math.Min(completedSteps, stepsToCeiling);

        return (Urgency)((int)initialUrgency + steps);
    }
}