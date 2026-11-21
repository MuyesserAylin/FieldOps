
namespace FieldOps.Api.Assignment;

//Bir ekibin dört ölçütten aldığı notlar (her biri 0-100) ve ağırlıklı toplam puanı (0-100).
//Notlar ayrı tutulur: karar gerekçesi kaydına "neden bu ekip" diye yazılacak.

public sealed record ScoreBreakdown(
    double Distance,   //Mesafe Notu
    double Workload,   //İş Yükü Notu
    double Skill,      //Beceri Notu
    double Response,   //Geçmiş Cevap Notu
    double Total);
