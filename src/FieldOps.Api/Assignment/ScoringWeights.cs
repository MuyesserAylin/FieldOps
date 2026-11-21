using System;

namespace FieldOps.Api.Assignment;
// Bir ağırlık seti: dört ölçütün puandaki payı.
// Sayılar appsettings.json'dan gelir.
public sealed class ScoringWeights
{
    public double Distance { get; set; }
    public double Workload { get; set; }
    public double Skill { get; set; }
    public double Response { get; set; }

    // Dört ağırlığın toplamı. Sadece okunur, ayardan doldurulmaz.
    public double Total => Distance + Workload + Skill + Response;

}
