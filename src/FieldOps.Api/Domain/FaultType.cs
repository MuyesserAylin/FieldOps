using System;

namespace FieldOps.Api.Domain;

public class FaultType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int RequiredSkillId { get; set; }
    public Skill RequiredSkill { get; set; } = null!;
    public int EstimatedDurationMinutes { get; set; }
    public Urgency DefaultUrgency { get; set; }

}
