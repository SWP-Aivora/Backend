using Aivora.Services.Base;
using Aivora.Services.IdentityService;

namespace Aivora.Services.AdminService;

public interface IAdminService
{
    Task<IdentityService.Response.UserResponse> SuspendUserAsync(Guid adminId, Guid userId, string reason);
    Task<IdentityService.Response.UserResponse> UnsuspendUserAsync(Guid adminId, Guid userId);
    Task<Aivora.Services.Base.Response.PageResult<IdentityService.Response.UserResponse>> GetUsersAsync(Aivora.Services.Base.Request.PageRequest pageRequest, string? search = null);
}

public class Request
{
    public class SuspendUserRequest
    {
        public string Reason { get; set; } = null!;
    }
}
