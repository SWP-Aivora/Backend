namespace Aivora.Services.DisputeService;

public class Response
{
    public class DisputeResponse
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string ProjectTitle { get; set; } = null!;
        public Guid MilestoneId { get; set; }
        public string MilestoneTitle { get; set; } = null!;
        public Guid OpenedBy { get; set; }
        public string OpenerName { get; set; } = null!;
        public Guid AgainstUserId { get; set; }
        public string AgainstUserName { get; set; } = null!;
        public string Reason { get; set; } = null!;
        public string? Description { get; set; }
        public string Status { get; set; } = null!;
        public string? ResolutionType { get; set; }
        public string? ResolutionNote { get; set; }
        public DateTimeOffset? ResolvedAt { get; set; }
        public List<DisputeEvidenceResponse> Evidence { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class DisputeEvidenceResponse
    {
        public Guid Id { get; set; }
        public Guid SubmittedBy { get; set; }
        public string SubmittedByName { get; set; } = null!;
        public string Content { get; set; } = null!;
        public string? FileUrl { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
