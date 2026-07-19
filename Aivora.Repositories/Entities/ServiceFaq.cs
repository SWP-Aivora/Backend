using Aivora.Repositories.Abstractions;

namespace Aivora.Repositories.Entities;

public class ServiceFaq : AuditableBaseEntity
{
    public Guid ServiceId { get; set; }
    public string Question { get; set; } = null!;
    public string Answer { get; set; } = null!;

    // Navigation Properties
    public virtual ServiceListing Service { get; set; } = null!;
}
