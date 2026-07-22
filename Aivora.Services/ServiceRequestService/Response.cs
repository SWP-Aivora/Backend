using Aivora.Repositories.Enums;

namespace Aivora.Services.ServiceRequestService;

public class Response
{
    public class ServiceRequestResponse
    {
        public Guid Id { get; set; }
        public Guid ServiceId { get; set; }
        public string ServiceTitle { get; set; } = null!;
        public Guid ExpertId { get; set; }
        public string ExpertName { get; set; } = null!;
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = null!;
        public Guid PackageId { get; set; }
        public string PackageTitle { get; set; } = null!;
        public decimal PackagePrice { get; set; }
        public int PackageDeliveryDays { get; set; }
        public string? Note { get; set; }
        public ServiceRequestStatus Status { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
