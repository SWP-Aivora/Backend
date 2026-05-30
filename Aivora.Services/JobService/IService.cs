using Aivora.Services.Base;

namespace Aivora.Services.JobService;

public interface IService
{
    Task<Response.JobResponse> GetJobByIdAsync(Guid id);
    Task<Response.JobResponse> CreateJobAsync(Guid clientId, Request.CreateJobRequest request);
    Task<Response.JobResponse> UpdateJobAsync(Guid clientId, Guid jobId, Request.UpdateJobRequest request);
    Task<bool> DeleteJobAsync(Guid clientId, Guid jobId);
    Task<Response.JobResponse> PublishJobAsync(Guid clientId, Guid jobId);
    Task<Aivora.Services.Base.Response.PageResult<Response.JobResponse>> GetJobsAsync(Request.PageRequest pageRequest, Guid? categoryId = null);
}
