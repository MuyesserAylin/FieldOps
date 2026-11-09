namespace FieldOps.Api.Reports;

public static class StatusLabels
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["Queued"] = "Bildirim alındı, ekip bekleniyor",
        ["Offered"] = "Ekip atanıyor",
        ["OnRoute"] = "Ekip yolda",
        ["OnSite"] = "Ekip sahada",
        ["Completed"] = "Tamamlandı",
        ["Cancelled"] = "İptal edildi"
    };

    public static string For(string status)
        => Labels.TryGetValue(status, out var label) ? label : status;
}