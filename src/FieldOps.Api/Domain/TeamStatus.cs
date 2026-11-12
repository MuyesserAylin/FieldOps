namespace FieldOps.Api.Domain;

public enum TeamStatus
{
    Idle, //Bosta Eleme aşamasında yalnızca Idle olan ekipler aday olabilecek.
    OfferPending,//Teklif Bekliyor
    OnRoute,//Yolda
    OnSite,//İşte
    Offline //Çevrimdışı
}
