using Aivora.Services.Base;

namespace Aivora.Services.ProposalService;

public interface IService
{
    Task<Response.ProposalResponse> GetProposalByIdAsync(Guid id);
    Task<Response.ProposalResponse> CreateProposalAsync(Guid expertId, Request.CreateProposalRequest request);
    Task<List<Response.ProposalResponse>> GetProposalsByJobIdAsync(Guid userId, Guid jobId);
    Task<List<Response.ProposalResponse>> GetExpertProposalsAsync(Guid expertId);
    Task<Response.ProposalResponse> UpdateProposalAsync(Guid expertId, Guid proposalId, Request.UpdateProposalRequest request);
    Task<Response.ProposalResponse> ResubmitProposalAsync(Guid expertId, Guid proposalId, Request.UpdateProposalRequest request);
}
