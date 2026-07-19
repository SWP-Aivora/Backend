using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class ServiceRequest : AuditableBaseEntity
{
    public Guid ServiceId { get; set; }
    public Guid ClientId { get; set; }
    public Guid PackageId { get; set; }
    public string? Note { get; set; }
    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.PENDING;

    // Snapshot of the package at the time of the request, so a later Service edit
    // (see ServiceCatalogService.UpdateServiceAsync) can't retroactively change the
    // price/terms of a request the Client already sent — mirrors how AcceptProposalAsync
    // copies ProposalMilestone -> Milestone instead of referencing it live.
    public string PackageTitle { get; set; } = null!;
    public decimal PackagePrice { get; set; }
    public int PackageDeliveryDays { get; set; }

    // Navigation Properties
    public virtual ServiceListing Service { get; set; } = null!;
    public virtual User Client { get; set; } = null!;
    public virtual ServicePackage Package { get; set; } = null!;
}
