using System.Globalization;

namespace FieldOps.Api.Teams;

public static class SkillLevelLabels
{
    public static string Get(int level) => level switch
    {
        1 => "Temel",
        2 => "Orta",
        3 => "Uzman",
        _ => level.ToString(CultureInfo.InvariantCulture)
    };
}