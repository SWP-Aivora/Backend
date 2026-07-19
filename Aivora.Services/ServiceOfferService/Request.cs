namespace Aivora.Services.ServiceOfferService;

public class Request
{
    public class CreateServiceOfferRequest
    {
        public decimal Amount { get; set; }
        public List<CreateServiceOfferMilestoneRequest> Milestones { get; set; } = new();
    }

    public class CreateServiceOfferMilestoneRequest
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public int DueDays { get; set; }
        public string? AcceptanceCriteria { get; set; }
        public int OrderIndex { get; set; }
    }
}
