namespace Aivora.Repositories.Entities;

public class JobSkill : BaseEntity
{
    public Guid JobId { get; set; }
    public Guid SkillId { get; set; }
    public bool IsRequired { get; set; } = true;

    // Navigation Properties
    public virtual JobPost Job { get; set; } = null!;
    public virtual Skill Skill { get; set; } = null!;
}
