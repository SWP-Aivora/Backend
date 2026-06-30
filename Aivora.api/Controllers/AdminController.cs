using Aivora.api.Extensions;
using Aivora.Services.AdminService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = JwtExtensions.AdminPolicy)]
[EnableRateLimiting("General")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var result = await _adminService.GetDashboardStatsAsync();
        return Ok(ApiResponseFactory.SuccessResponse(result, "Dashboard stats retrieved", HttpContext.TraceIdentifier));
    }

    [HttpGet("expert-reviews")]
    [Authorize(Policy = JwtExtensions.AdminPolicy)]
    public async Task<IActionResult> GetExpertReviews([FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest, [FromQuery] string? search)
    {
        var result = await _adminService.GetExpertReviewsAsync(pageRequest, search);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Expert reviews retrieved", HttpContext.TraceIdentifier));
    }

    [HttpGet("users")]
    [Authorize(Policy = JwtExtensions.AdminPolicy)]
    public async Task<IActionResult> GetUsers([FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest, [FromQuery] string? search)
    {
        var result = await _adminService.GetUsersAsync(pageRequest, search);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Users retrieved", HttpContext.TraceIdentifier));
    }

    [HttpPut("users/{id}/suspend")]
    public async Task<IActionResult> SuspendUser(Guid id, [FromBody] Request.SuspendUserRequest request)
    {
        var adminId = this.GetUserId();
        var result = await _adminService.SuspendUserAsync(adminId, id, request.Reason);
        return Ok(ApiResponseFactory.SuccessResponse(result, "User suspended successfully", HttpContext.TraceIdentifier));
    }

    [HttpPut("users/{id}/unsuspend")]
    public async Task<IActionResult> UnsuspendUser(Guid id)
    {
        var adminId = this.GetUserId();
        var result = await _adminService.UnsuspendUserAsync(adminId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "User unsuspended successfully", HttpContext.TraceIdentifier));
    }
}
