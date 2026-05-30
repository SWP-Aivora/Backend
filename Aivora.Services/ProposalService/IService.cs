using Aivora.Services.Base;

namespace Aivora.Services.ProposalService;

public interface IService
{
    Task<Response.ProposalResponse> GetProposalByIdAsync(Guid id);
    Task<Response.ProposalResponse> CreateProposalAsync(Guid expertId, Request.CreateProposalRequest request);
    Task<Response.ProposalResponse> UpdateProposalStatusAsync(Guid userId, Guid proposalId, Repositories.Enums.ProposalStatus status);
    Task<List<Response.ProposalResponse>> GetProposalsByJobIdAsync(Guid userId, Guid jobId);
    Task<List<Response.ProposalResponse>> GetExpertProposalsAsync(Guid expertId);
}
