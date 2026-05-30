namespace Aivora.Repositories.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }

    // Navigation Properties
    public virtual Category? Parent { get; set; }
    public virtual ICollection<Category> SubCategories { get; set; } = new List<Category>();
    public virtual ICollection<Skill> Skills { get; set; } = new List<Skill>();
}
