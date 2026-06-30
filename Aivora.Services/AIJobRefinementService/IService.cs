namespace Aivora.Services.AIJobRefinementService;

public interface IService
{
    Task<Response.JobRefinementResponse> RefineJobAsync(Guid clientId, Guid jobId, Request.RefineJobRequest request, CancellationToken cancellationToken = default);
}
