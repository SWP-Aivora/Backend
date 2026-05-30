namespace Aivora.Services.ReviewService;

public class Response
{
    public class ReviewResponse
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public Guid ReviewerId { get; set; }
        public string ReviewerName { get; set; } = null!;
        public Guid RevieweeId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public int? CommunicationRating { get; set; }
        public int? QualityRating { get; set; }
        public int? DeadlineRating { get; set; }
        public int? RequirementClarityRating { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
