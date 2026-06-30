namespace Aivora.Services.AIJobRefinementService;

public class Response
{
    public class JobRefinementResponse
    {
        public JobService.Response.JobResponse Job { get; set; } = null!;
        public string AIResponse { get; set; } = null!;
        public List<string> ChangedFields { get; set; } = new();
    }
}
