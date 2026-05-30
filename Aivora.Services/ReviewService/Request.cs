namespace Aivora.Services.ReviewService;

public class Request
{
    public class CreateReviewRequest
    {
        public Guid ProjectId { get; set; }
        public Guid RevieweeId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public int? CommunicationRating { get; set; }
        public int? QualityRating { get; set; }
        public int? DeadlineRating { get; set; }
        public int? RequirementClarityRating { get; set; }
    }
}
