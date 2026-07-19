using Aivora.Repositories.Enums;

namespace Aivora.Services.ServiceCatalogService;

public class Response
{
    public class ServiceResponse
    {
        public Guid Id { get; set; }
        public Guid ExpertId { get; set; }
        public string ExpertName { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public ServiceStatus Status { get; set; }
        public string? AttachmentUrl { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public List<PackageResponse> Packages { get; set; } = new();
        public List<FaqResponse> Faqs { get; set; } = new();
    }

    public class PackageResponse
    {
        public Guid Id { get; set; }
        public PackageTier Tier { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int DeliveryDays { get; set; }
        public string? Features { get; set; }
    }

    public class FaqResponse
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = null!;
        public string Answer { get; set; } = null!;
    }
}
