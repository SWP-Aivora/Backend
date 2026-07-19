using Aivora.Repositories.Enums;

namespace Aivora.Services.JobInviteService;

public interface IService
{
    Task<Response.JobInviteResponse> CreateInviteAsync(Guid clientId, Guid jobId, Request.CreateJobInviteRequest request);
    Task<Response.JobInviteResponse> AcceptInviteAsync(Guid expertId, Guid inviteId);
    Task<Response.JobInviteResponse> DeclineInviteAsync(Guid expertId, Guid inviteId);
    Task<List<Response.JobInviteResponse>> GetInvitesByJobAsync(Guid clientId, Guid jobId);
    Task<List<Response.JobInviteResponse>> GetMyInvitesForExpertAsync(Guid expertId, JobInviteStatus? status);
}
