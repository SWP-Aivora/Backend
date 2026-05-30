using Aivora.Repositories.Enums;

namespace Aivora.Services.DisputeService;

public class Request
{
    public class OpenDisputeRequest
    {
        public Guid MilestoneId { get; set; }
        public string Reason { get; set; } = null!;
        public string? Description { get; set; }
    }

    public class AddEvidenceRequest
    {
        public string Content { get; set; } = null!;
        public string? FileUrl { get; set; }
    }

    public class ResolveDisputeRequest
    {
        public DisputeResolutionType ResolutionType { get; set; }
        public string ResolutionNote { get; set; } = null!;
        public decimal? ReleaseAmount { get; set; }
        public decimal? RefundAmount { get; set; }
    }
}
