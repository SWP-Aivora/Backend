namespace Aivora.Services.DeliverableService;

public interface IService
{
    Task<Response.DeliverableResponse> SubmitDeliverableAsync(Guid expertId, Guid milestoneId, Request.SubmitDeliverableRequest request);
    Task<List<Response.DeliverableResponse>> GetDeliverablesByMilestoneAsync(Guid userId, Guid milestoneId);
}
