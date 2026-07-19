using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class ServicePackage : AuditableBaseEntity
{
    public Guid ServiceId { get; set; }
    public PackageTier Tier { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DeliveryDays { get; set; }
    public string? Features { get; set; }

    // Navigation Properties
    public virtual ServiceListing Service { get; set; } = null!;
}
