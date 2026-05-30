using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Aivora.api.Extensions;

public static class ControllerExtensions
{
    public static Guid GetUserId(this ControllerBase controller)
    {
        var userIdClaim = controller.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            throw new UnauthorizedAccessException("User ID claim not found.");
        }

        return Guid.Parse(userIdClaim.Value);
    }
}
