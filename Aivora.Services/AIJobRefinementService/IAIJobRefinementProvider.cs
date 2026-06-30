namespace Aivora.Services.AIJobRefinementService;

public interface IAIJobRefinementProvider
{
    Task<AIJobRefinementDraft> RefineJobAsync(JobService.Response.JobResponse currentJob, string message, CancellationToken cancellationToken = default);
}
