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
    private readonly Aivora.Services.ExpertVerificationService.IService _expertVerificationService;

    public AdminController(IAdminService adminService, Aivora.Services.ExpertVerificationService.IService expertVerificationService)
    {
        _adminService = adminService;
        _expertVerificationService = expertVerificationService;
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

    [HttpGet("expert-profile-updates")]
    [Authorize(Policy = JwtExtensions.AdminPolicy)]
    public async Task<IActionResult> GetExpertProfileUpdates([FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest, [FromQuery] string? status)
    {
        var result = await _adminService.GetExpertProfileUpdatesAsync(pageRequest, status);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Expert profile updates retrieved", HttpContext.TraceIdentifier));
    }

    [HttpGet("expert-profile-updates/{id}")]
    [Authorize(Policy = JwtExtensions.AdminPolicy)]
    public async Task<IActionResult> GetExpertProfileUpdateById(Guid id)
    {
        var result = await _adminService.GetExpertProfileUpdateByIdAsync(id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Expert profile update retrieved", HttpContext.TraceIdentifier));
    }

    [HttpPut("expert-profile-updates/{id}/review")]
    [Authorize(Policy = JwtExtensions.AdminPolicy)]
    public async Task<IActionResult> ReviewExpertProfileUpdate(Guid id, [FromBody] Request.ReviewExpertProfileUpdateRequest request)
    {
        var adminId = this.GetUserId();
        var result = await _adminService.ReviewExpertProfileUpdateAsync(adminId, id, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Expert profile update reviewed", HttpContext.TraceIdentifier));
    }

    [HttpGet("expert-verifications")]
    [Authorize(Policy = JwtExtensions.AdminPolicy)]
    public async Task<IActionResult> GetExpertVerifications([FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest, [FromQuery] Aivora.Repositories.Enums.ExpertVerificationStatus? status, [FromQuery] Guid? expertId)
    {
        var result = await _expertVerificationService.GetAdminVerificationsAsync(pageRequest, status, expertId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Expert verifications retrieved", HttpContext.TraceIdentifier));
    }

    [HttpPut("expert-verifications/{id}/review")]
    [Authorize(Policy = JwtExtensions.AdminPolicy)]
    public async Task<IActionResult> ReviewExpertVerification(Guid id, [FromBody] Aivora.Services.ExpertVerificationService.Request.ReviewVerificationRequest request)
    {
        var adminId = this.GetUserId();
        var result = await _expertVerificationService.ReviewEscalatedVerificationAsync(adminId, id, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Expert verification reviewed", HttpContext.TraceIdentifier));
    }
}
