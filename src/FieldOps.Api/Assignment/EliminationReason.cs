namespace FieldOps.Api.Assignment;

//bir ekibin neden aday olmadığı 
//karar kaydında tek sütun olarak tutulur
public enum EliminationReason
{
    MissingSkill,     //İşin gerektirdiği beceriye sahip değil
    NotOnShift,       //Vardiyada değil
    NotIdle,          //Durumu 'Idle' değil
    StaleLocation,    //Güncel konum bilgisi çok eski
    TooFar,           //Mesafe sınırından uzak  
    RejectedThisJob,  //Bu işi daha önce reddetmiş veya yarıda bırakmış
    CoolingDown       //Yakın zamanda teklifi cevapsız bırakmış,bekletmede
}

