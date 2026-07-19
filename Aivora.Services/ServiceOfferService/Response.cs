using Aivora.Repositories.Enums;

namespace Aivora.Services.ServiceOfferService;

public class Response
{
    public class ServiceOfferResponse
    {
        public Guid Id { get; set; }
        public Guid ServiceRequestId { get; set; }
        public Guid ExpertId { get; set; }
        public decimal Amount { get; set; }
        public ServiceOfferStatus Status { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public List<ServiceOfferMilestoneResponse> Milestones { get; set; } = new();
    }

    public class ServiceOfferMilestoneResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public int DueDays { get; set; }
        public string? AcceptanceCriteria { get; set; }
        public int OrderIndex { get; set; }
    }

    public class AcceptServiceOfferResultResponse
    {
        public Guid ProjectId { get; set; }
        public Guid ServiceOfferId { get; set; }
        public string Status { get; set; } = null!;
    }
}
