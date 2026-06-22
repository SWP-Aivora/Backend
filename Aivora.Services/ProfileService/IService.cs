using Aivora.Services.IdentityService;

namespace Aivora.Services.ProfileService;

public interface IService
{
    Task<Response.ClientProfileResponse> GetClientProfileAsync(Guid userId);
    Task<Response.ClientProfileResponse> UpdateClientProfileAsync(Guid userId, Request.UpdateClientProfileRequest request);

    Task<Response.ExpertProfileResponse> GetExpertProfileAsync(Guid userId);
    Task<Response.ExpertProfileResponse> UpdateExpertProfileAsync(Guid userId, Request.UpdateExpertProfileRequest request);

    Task<Aivora.Services.IdentityService.Response.UserResponse> UpdateUserAsync(Guid userId, Request.UpdateUserRequest request);
    Task<Response.ExpertProfileResponse> GetPublicExpertProfileAsync(Guid expertId);
    Task<List<Response.ExpertProfileResponse>> GetFeaturedExpertsAsync(int count);
    Task<Response.PaginatedExpertListResponse> SearchExpertsAsync(Request.SearchExpertsRequest request);
}
