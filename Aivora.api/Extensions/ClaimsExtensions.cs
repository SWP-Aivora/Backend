using System.Security.Claims;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;

namespace Aivora.api.Extensions;

public static class ClaimsExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            throw new UnauthorizedException("User ID not found in token.");

        return Guid.Parse(userIdClaim.Value);
    }

    public static UserRole GetUserRole(this ClaimsPrincipal principal)
    {
        var roleClaim = principal.FindFirst(ClaimTypes.Role);
        if (roleClaim == null)
            throw new UnauthorizedException("User role not found in token.");

        return Enum.Parse<UserRole>(roleClaim.Value, true);
    }
}
