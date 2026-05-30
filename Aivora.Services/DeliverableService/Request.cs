namespace Aivora.Services.DeliverableService;

public class Request
{
    public class SubmitDeliverableRequest
    {
        public string Description { get; set; } = null!;
        public string? FileUrl { get; set; }
        public string? DemoUrl { get; set; }
        public string? SourceCodeUrl { get; set; }
        public string? Note { get; set; }
    }
}
