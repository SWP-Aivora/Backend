using Aivora.Repositories.Enums;

namespace Aivora.Services.DeliverableService;

public class Response
{
    public class DeliverableResponse
    {
        public Guid Id { get; set; }
        public Guid MilestoneId { get; set; }
        public Guid ExpertId { get; set; }
        public string Description { get; set; } = null!;
        public string? FileUrl { get; set; }
        public string? DemoUrl { get; set; }
        public string? SourceCodeUrl { get; set; }
        public string? Note { get; set; }
        public int RevisionNumber { get; set; }
        public DeliverableStatus Status { get; set; }
        public DateTimeOffset SubmittedAt { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }
    }
}
