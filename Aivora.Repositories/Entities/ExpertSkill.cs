using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class ExpertSkill : AuditableBaseEntity
{
    public Guid ExpertId { get; set; }
    public Guid SkillId { get; set; }
    public SkillLevel Level { get; set; } = SkillLevel.INTERMEDIATE;
    public int YearsExperience { get; set; }

    // Navigation Properties
    public virtual ExpertProfile Expert { get; set; } = null!;
    public virtual Skill Skill { get; set; } = null!;
}

