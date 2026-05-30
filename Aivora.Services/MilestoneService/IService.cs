namespace Aivora.Services.MilestoneService;

public interface IService
{
    Task<Response.MilestoneResponse> GetMilestoneByIdAsync(Guid userId, Guid milestoneId);
    Task<Response.MilestoneResponse> CreateMilestoneAsync(Guid userId, Guid projectId, Request.CreateMilestoneRequest request);
    Task<Response.MilestoneResponse> UpdateMilestoneAsync(Guid userId, Guid milestoneId, Request.UpdateMilestoneRequest request);
    Task<Response.FundResultResponse> FundMilestoneAsync(Guid userId, Guid milestoneId);
    Task<Response.MilestoneResponse> ApproveMilestoneAsync(Guid userId, Guid milestoneId);
    Task<Response.MilestoneResponse> RequestRevisionAsync(Guid userId, Guid milestoneId, string reason);
    Task<bool> OpenDisputeAsync(Guid userId, Guid milestoneId, string reason);
}
