using FieldOps.Api.Domain;

namespace FieldOps.Api.Assignment;

// Kuyrukta bekleyen işin güncel aciliyetini hesaplar 
// Kural: iş kuyrukta her UrgencyEscalationMinutes dakikada bir basamak yükselir; tavan "yüksek"tir.
// Başlangıçta yüksek ya da kritik olan iş hiç değişmez. "Kritik" yalnızca arıza türünden ya da operatörden gelir;
// beklemiş bir sokak lambası gaz kaçağıyla aynı seviyeye çıkmamalı.
// Amaç: düşük öncelikli iş yoğun günde sonsuza kadar aç kalmasın.
// Her zaman BAŞLANGIÇ aciliyetinden hesaplanır; böylece aynı girdi aynı sonucu verir ve
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

        if (initialUrgency >= Urgency.High)
        {
            return initialUrgency;
        }

        var waited = now - queuedSince;
        if (waited <= TimeSpan.Zero)
        {
            return initialUrgency;
        }

        var completedSteps = Math.Floor(waited.TotalMinutes / settings.UrgencyEscalationMinutes);
        var stepsToCeiling = (int)Urgency.High - (int)initialUrgency;
        var steps = (int)Math.Min(completedSteps, stepsToCeiling);

        return (Urgency)((int)initialUrgency + steps);
    }
}