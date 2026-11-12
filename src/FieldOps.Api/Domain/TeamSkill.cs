
namespace FieldOps.Api.Domain;

public class TeamSkill
{
    private TeamSkill() { }

    public TeamSkill(int skillId, TeamSkillLevel level)
    {
        if (!Enum.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Geçersiz beceri seviyesi.");
        }
        SkillId = skillId;
        Level = level;
    }

    public int TeamId { get; private set; }
    public Team Team { get; private set; } = null!;
    public int SkillId { get; private set; }
    public Skill Skill { get; private set; } = null!;
    public TeamSkillLevel Level { get; private set; }

}
