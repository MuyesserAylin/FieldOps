namespace FieldOps.Api.Reports;

public static class FaultTypeLabels
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["Gas leak"] = "Doğalgaz kaçağı",
        ["Burst water pipe"] = "Su borusu patlağı",
        ["Power outage"] = "Elektrik kesintisi",
        ["Pothole"] = "Yol çukuru",
        ["Street light failure"] = "Sokak lambası arızası"
    };

    public static string For(string name)
        => Labels.TryGetValue(name, out var label) ? label : name;
}