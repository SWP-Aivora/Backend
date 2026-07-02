using Microsoft.AspNetCore.Http;

namespace Aivora.Services.ExpertVerificationService;

public class Request
{
    public class SubmitEvidenceRequest
    {
        public Guid ExpertSkillId { get; set; }
        public IFormFile File { get; set; } = null!;
    }

    public class ReviewVerificationRequest
    {
        public bool IsApproved { get; set; }
        public string? RejectionReason { get; set; }
    }
}
