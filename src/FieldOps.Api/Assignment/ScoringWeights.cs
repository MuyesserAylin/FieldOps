using System;

namespace FieldOps.Api.Assignment;

public sealed class ScoringWeights
{
    public double Distance { get; set; }
    public double Workload { get; set; }
    public double Skill { get; set; }
    public double Response { get; set; }

    // Dört ağırlığın toplamı. Sadece okunur, ayardan doldurulmaz.
    public double Total => Distance + Workload + Skill + Response;

}
