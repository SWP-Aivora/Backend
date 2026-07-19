using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class ServiceOffer : AuditableBaseEntity
{
    public Guid ServiceRequestId { get; set; }
    public Guid ExpertId { get; set; }
    public decimal Amount { get; set; }
    public ServiceOfferStatus Status { get; set; } = ServiceOfferStatus.PENDING;

    // Navigation Properties
    public virtual ServiceRequest ServiceRequest { get; set; } = null!;
    public virtual User Expert { get; set; } = null!;
    public virtual ICollection<ServiceOfferMilestone> Milestones { get; set; } = new List<ServiceOfferMilestone>();
}
