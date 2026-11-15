namespace FieldOps.Api.Teams;

public static class SkillLabels
{
    public static string Get(string skill) => skill switch
    {
        "Gas" => "Doğalgaz",
        "Plumbing" => "Su tesisatı",
        "Electrical" => "Elektrik",
        "RoadWork" => "Yol",
        _ => skill
    };
}