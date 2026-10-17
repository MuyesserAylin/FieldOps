namespace FieldOps.Api.Domain;

public enum JobStatus
{
    Queued, //Kuyrukta
    Offered, //Teklifte
    OnRoute, //Yolda
    OnSite,  //İşte
    Completed, //Tamamlandı
    Cancelled //İptal Edildi
}
