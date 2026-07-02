using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Aivora.Services.Exceptions;

namespace Aivora.Services.JwtService;

public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid GetCurrentUserId()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedException("User not found");
        return Guid.Parse(userId);
    }

    public string GetUserRole()
    {
        var role = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrEmpty(role))
            throw new UnauthorizedException("User role not found");
        return role;
    }

    public Guid GetExpertId()
    {
        return GetCurrentUserId();
    }

    public Guid GetClientId()
    {
        return GetCurrentUserId();
    }

    public Guid GetAdminId()
    {
        return GetCurrentUserId();
    }
}