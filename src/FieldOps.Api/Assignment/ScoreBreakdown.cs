using System;

namespace FieldOps.Api.Assignment;
//Bir ekibin dört ölçütten aldığı notlar (her biri 0-100) ve ağırlıklı toplam puanı (0-100).
// Notlar ayrı tutulur: karar gerekçesi kaydına "neden bu ekip" diye yazılacak.
public sealed record ScoreBreakdown(
    double Distance,
    double Workload,
    double Skill,
    double Response,
    double Total);
