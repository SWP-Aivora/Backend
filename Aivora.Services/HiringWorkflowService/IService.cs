namespace Aivora.Services.HiringWorkflowService;

public interface IService
{
    Task<Response.HiringResultResponse> AcceptProposalAsync(Guid currentUserId, Guid proposalId);
}
