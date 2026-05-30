using Aivora.Repositories.Abstractions;
namespace Aivora.Repositories.Entities;

public class Skill : AuditableBaseEntity
{
    public string Name { get; set; } = null!;
    public Guid? CategoryId { get; set; }

    // Navigation Properties
    public virtual Category? Category { get; set; }
}

