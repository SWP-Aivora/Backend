using Aivora.Services.Base;

namespace Aivora.Services.ReviewService;

public interface IService
{
    Task<Response.ReviewResponse> CreateReviewAsync(Guid reviewerId, Request.CreateReviewRequest request);
    Task<Aivora.Services.Base.Response.PageResult<Response.ReviewResponse>> GetUserReviewsAsync(Guid userId, Aivora.Services.Base.Request.PageRequest pageRequest);
}
