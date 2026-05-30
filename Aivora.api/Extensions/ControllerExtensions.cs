using System.Security.Claims;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Aivora.api.Extensions;

public static class ControllerExtensions
{
    public static Guid GetUserId(this ControllerBase controller)
    {
        var userIdClaim = controller.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            throw new UnauthorizedException("User ID not found in token.");

        return Guid.Parse(userIdClaim.Value);
    }

    public static UserRole GetUserRole(this ControllerBase controller)
    {
        var roleClaim = controller.User.FindFirst(ClaimTypes.Role);
        if (roleClaim == null)
            throw new UnauthorizedException("User role not found in token.");

        return Enum.Parse<UserRole>(roleClaim.Value, true);
    }
}
