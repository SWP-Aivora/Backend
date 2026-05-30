namespace Aivora.Services.MilestoneService;

public class Request
{
    public class CreateMilestoneRequest
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? AcceptanceCriteria { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "AICOIN";
        public DateOnly? DueDate { get; set; }
        public int OrderIndex { get; set; }
    }

    public class UpdateMilestoneRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? AcceptanceCriteria { get; set; }
        public decimal? Amount { get; set; }
        public DateOnly? DueDate { get; set; }
        public int? OrderIndex { get; set; }
    }
}
