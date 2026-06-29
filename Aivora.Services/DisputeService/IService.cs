namespace Aivora.Services.DisputeService;

public interface IService
{
    Task<Response.DisputeResponse> OpenDisputeAsync(Guid userId, Request.OpenDisputeRequest request);
    Task<Response.DisputeResponse> GetDisputeByIdAsync(Guid userId, Guid disputeId);
    Task<Base.Response.PageResult<Response.DisputeResponse>> GetDisputesAsync(Guid userId, string role, Base.Request.PageRequest pageRequest);
    Task<Response.DisputeEvidenceResponse> AddEvidenceAsync(Guid userId, Guid disputeId, Request.AddEvidenceRequest request);
    Task<Response.DisputeResponse> ResolveDisputeAsync(Guid adminId, Guid disputeId, Request.ResolveDisputeRequest request);
    Task<Response.DisputeResponse> CloseDisputeAsync(Guid userId, Guid disputeId);
    Task<Response.DisputeResponse> RequestEvidenceAsync(Guid adminId, Guid disputeId, Request.RequestEvidenceRequest request);
    Task DeleteEvidenceAsync(Guid userId, Guid disputeId, Guid evidenceId);
}
