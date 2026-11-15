namespace FieldOps.Api.Teams;

public static class TeamStatusLabels
{
    public static string Get(string status) => status switch
    {
        "Idle" => "Boşta",
        "OfferPending" => "Teklif bekliyor",
        "OnRoute" => "Yolda",
        "OnSite" => "İşte",
        "Offline" => "Çevrimdışı",
        _ => status
    };
}