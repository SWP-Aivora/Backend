namespace Aivora.Repositories.Abstractions;

public abstract class BaseEntity<TKey>
{
    public TKey Id { get; set; } = default!;

    public bool IsDeleted { get; set; } // Soft Delete
}

public abstract class BaseEntity : BaseEntity<Guid>
{
    protected BaseEntity()
    {
        Id = Guid.NewGuid();
    }
}

public abstract class AuditableBaseEntity : BaseEntity, IAuditableEntity
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
