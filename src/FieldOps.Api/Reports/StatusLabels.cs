namespace FieldOps.Api.Reports;

public static class StatusLabels
{
    // Vatandaşa gösterilen cümleler
    private static readonly Dictionary<string, string> CitizenLabels = new()
    {
        ["Queued"] = "Bildirim alındı, ekip bekleniyor",
        ["Offered"] = "Ekip atanıyor",
        ["OnRoute"] = "Ekip yolda",
        ["OnSite"] = "Ekip sahada",
        ["Completed"] = "Tamamlandı",
        ["Cancelled"] = "İptal edildi"
    };

    // Operatör tablosunda gösterilen kısa etiketler
    private static readonly Dictionary<string, string> OperatorLabels = new()
    {
        ["Queued"] = "Kuyrukta",
        ["Offered"] = "Teklifte",
        ["OnRoute"] = "Yolda",
        ["OnSite"] = "Sahada",
        ["Completed"] = "Tamamlandı",
        ["Cancelled"] = "İptal edildi"
    };

    private static readonly Dictionary<string, string> UrgencyLabels = new()
    {
        ["Low"] = "Düşük",
        ["Medium"] = "Orta",
        ["High"] = "Yüksek",
        ["Critical"] = "Kritik"
    };

    public static string For(string status)
        => CitizenLabels.TryGetValue(status, out var label) ? label : status;

    public static string ForOperator(string status)
        => OperatorLabels.TryGetValue(status, out var label) ? label : status;

    public static string ForUrgency(string urgency)
        => UrgencyLabels.TryGetValue(urgency, out var label) ? label : urgency;
}