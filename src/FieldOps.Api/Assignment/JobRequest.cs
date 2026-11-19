using FieldOps.Api.Domain;

namespace FieldOps.Api.Assignment;
//Urgency: işin GÜNCEL ACİLİYETİ (bekleme yükseltmesinden sonraki)
//RejectedTeamIds: bu işi daha önce reddeden veya yarıda bırakan ekipler
public sealed record JobRequest(
    long JobId,                         //karar kaydına hangi iş olduğunu yazmak için
    int RequiredSkillId,                //beceri var mı elemesi için  
    Urgency Urgency,                    //mesafe sınırını belirlemek için(kritik->yok, yüksek->%60, orta->%40, düşük->%20)
    IReadOnlySet<int> RejectedTeamIds); // bu işi daha önce reddetmiş mi  
