using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class ServiceListing : AuditableBaseEntity
{
    public Guid ExpertId { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public ServiceStatus Status { get; set; } = ServiceStatus.DRAFT;
    public string? AttachmentUrl { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    // Navigation Properties
    public virtual User Expert { get; set; } = null!;
    public virtual ICollection<ServicePackage> Packages { get; set; } = new List<ServicePackage>();
    public virtual ICollection<ServiceFaq> Faqs { get; set; } = new List<ServiceFaq>();
}
