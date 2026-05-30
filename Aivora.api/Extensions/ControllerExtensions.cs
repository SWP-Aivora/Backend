using Aivora.Repositories.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Aivora.api.Extensions;

public static class ControllerExtensions
{
    public static Guid GetUserId(this ControllerBase controller)
    {
        return controller.User.GetUserId();
    }

    public static UserRole GetUserRole(this ControllerBase controller)
    {
        return controller.User.GetUserRole();
    }
}
