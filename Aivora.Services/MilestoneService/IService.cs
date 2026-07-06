namespace Aivora.Services.MilestoneService;

public interface IService
{
    Task<Response.MilestoneResponse> GetMilestoneByIdAsync(Guid userId, Guid milestoneId);
    Task<Response.MilestoneResponse> CreateMilestoneAsync(Guid userId, Guid projectId, Request.CreateMilestoneRequest request);
    Task<Response.MilestoneResponse> UpdateMilestoneAsync(Guid userId, Guid milestoneId, Request.UpdateMilestoneRequest request);
    Task<Response.FundResultResponse> FundMilestoneAsync(Guid userId, Guid milestoneId);
    Task<Response.MilestoneResponse> ApproveMilestoneAsync(Guid userId, Guid milestoneId);
    Task<Response.MilestoneResponse> RequestRevisionAsync(Guid userId, Guid milestoneId, string reason);
    Task<List<Response.MilestoneStepResponse>> GetMilestoneStepsAsync(Guid userId, Guid milestoneId);
    Task<Response.MilestoneStepResponse> AddMilestoneStepAsync(Guid userId, Guid milestoneId, Request.CreateMilestoneStepRequest request);
    Task<Response.MilestoneStepResponse> UpdateMilestoneStepAsync(Guid userId, Guid stepId, Request.UpdateMilestoneStepRequest request);
    Task DeleteMilestoneStepAsync(Guid userId, Guid stepId);
    Task<Response.MilestoneStepResponse> UpdateStepStatusAsync(Guid userId, Guid stepId, Request.UpdateStepStatusRequest request);
    Task ReorderMilestoneStepsAsync(Guid userId, Guid milestoneId, List<Guid> stepIds);
}
