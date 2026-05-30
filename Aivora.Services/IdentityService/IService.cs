namespace Aivora.Services.IdentityService;

public interface IService
{
    Task<Response.IdentityResponse> LoginAsync(Request.LoginRequest request);
    Task<Response.IdentityResponse> RegisterAsync(Request.RegisterRequest request);
    Task<Response.IdentityResponse> RefreshTokenAsync(Request.RefreshTokenRequest request);
    Task<Response.UserResponse> GetCurrentUserAsync(Guid userId);
}
