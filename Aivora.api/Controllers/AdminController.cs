using Aivora.api.Extensions;
using Aivora.Services.AdminService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = JwtExtensions.AdminPolicy)]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest, [FromQuery] string? search)
    {
        var result = await _adminService.GetUsersAsync(pageRequest, search);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Users retrieved", HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}/suspend")]
    public async Task<IActionResult> SuspendUser(Guid id, [FromBody] Request.SuspendUserRequest request)
    {
        var adminId = this.GetUserId();
        var result = await _adminService.SuspendUserAsync(adminId, id, request.Reason);
        return Ok(ApiResponseFactory.SuccessResponse(result, "User suspended successfully", HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}/unsuspend")]
    public async Task<IActionResult> UnsuspendUser(Guid id)
    {
        var adminId = this.GetUserId();
        var result = await _adminService.UnsuspendUserAsync(adminId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "User unsuspended successfully", HttpContext.TraceIdentifier));
    }
}
