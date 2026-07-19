using Aivora.Repositories.Enums;

namespace Aivora.Services.ServiceCatalogService;

public class Request
{
    public class CreateServiceRequest
    {
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string? AttachmentUrl { get; set; }
        public List<PackageRequest> Packages { get; set; } = new();
        public List<FaqRequest> Faqs { get; set; } = new();
    }

    public class UpdateServiceRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? AttachmentUrl { get; set; }
        public List<PackageRequest>? Packages { get; set; }
        public List<FaqRequest>? Faqs { get; set; }
    }

    public class PackageRequest
    {
        public PackageTier Tier { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int DeliveryDays { get; set; }
        public string? Features { get; set; }
    }

    public class FaqRequest
    {
        public string Question { get; set; } = null!;
        public string Answer { get; set; } = null!;
    }
}
