using Aivora.Repositories.Enums;

namespace Aivora.Services.ExpertVerificationService;

public interface IService
{
    Task<Response.ExpertVerificationResponse> SubmitEvidenceAsync(Guid expertUserId, Request.SubmitEvidenceRequest request, CancellationToken cancellationToken = default);
    Task<Base.Response.PageResult<Response.ExpertVerificationResponse>> GetMyVerificationsAsync(Guid expertUserId, Guid? expertSkillId, Base.Request.PageRequest pageRequest);
    Task<Response.ExpertVerificationResponse> EscalateAsync(Guid expertUserId, Guid verificationId);
    Task<Base.Response.PageResult<Response.ExpertVerificationResponse>> GetAdminVerificationsAsync(Base.Request.PageRequest pageRequest, ExpertVerificationStatus? status, Guid? expertId);
    Task<Response.ExpertVerificationResponse> ReviewEscalatedVerificationAsync(Guid adminId, Guid verificationId, Request.ReviewVerificationRequest request);
}
