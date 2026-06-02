using Aivora.Services.Base;

namespace Aivora.Services.HiringService;

/// <summary>
/// IHiringService - Module chịu trách nhiệm về toàn bộ vòng đời thuê mướn (Hiring Lifecycle).
/// Đây là một "Deep Module" hợp nhất các trạng thái phân mảnh giữa Job, Proposal và Project.
/// </summary>
public interface IHiringService
{
    /// <summary>
    /// Client chấp nhận một Proposal. 
    /// Hành động này là Atomic: Proposal -> ACCEPTED, Các Proposal khác -> REJECTED, Job -> IN_PROGRESS, Project -> CREATED.
    /// </summary>
    Task<Response.HiringResultResponse> AcceptProposalAsync(Guid clientId, Guid proposalId);

    /// <summary>
    /// Client đưa một Proposal vào danh sách rút gọn.
    /// </summary>
    Task<bool> ShortlistProposalAsync(Guid clientId, Guid proposalId);

    /// <summary>
    /// Client từ chối một Proposal.
    /// </summary>
    Task<bool> RejectProposalAsync(Guid clientId, Guid proposalId);

    /// <summary>
    /// Expert tự rút lại Proposal của mình.
    /// </summary>
    Task<bool> WithdrawProposalAsync(Guid expertId, Guid proposalId);
}
