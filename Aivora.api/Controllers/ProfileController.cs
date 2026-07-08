using Aivora.api.Extensions;
using Aivora.Services.Models;
using Aivora.Services.ProfileService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/profiles")]
[Authorize]
[EnableRateLimiting("General")]
public class ProfileController : ControllerBase
{
    private readonly IService _profileService;

    public ProfileController(IService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet("client")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> GetClientProfile()
    {
        var userId = this.GetUserId();
        var result = await _profileService.GetClientProfileAsync(userId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Client profile retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpPut("client")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> UpdateClientProfile([FromBody] Request.UpdateClientProfileRequest request)
    {
        var userId = this.GetUserId();
        var result = await _profileService.UpdateClientProfileAsync(userId, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Client profile updated successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("expert")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> GetExpertProfile()
    {
        var userId = this.GetUserId();
        var result = await _profileService.GetExpertProfileAsync(userId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Expert profile retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpPut("expert")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> UpdateExpertProfile([FromBody] Request.UpdateExpertProfileRequest request)
    {
        var userId = this.GetUserId();
        var result = await _profileService.UpdateExpertProfileAsync(userId, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Expert profile updated successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("expert/{expertId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetExpertPublic(Guid expertId)
    {
        var result = await _profileService.GetPublicExpertProfileAsync(expertId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Expert public profile retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("experts/featured")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFeaturedExperts([FromQuery] int count = 5)
    {
        var result = await _profileService.GetFeaturedExpertsAsync(count);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Featured experts retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("experts/search")]
    [AllowAnonymous]
    public async Task<IActionResult> SearchExperts(
        [FromQuery] string? keyword = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 10)
    {
        var request = new Aivora.Services.ProfileService.Request.SearchExpertsRequest
        {
            Keyword = keyword,
            CategoryId = categoryId,
            Page = page,
            PageSize = pageSize
        };
        var result = await _profileService.SearchExpertsAsync(request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Experts searched successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("expert/{expertId}/completed-projects")]
    [AllowAnonymous]
    public async Task<IActionResult> GetExpertCompletedProjects(
        Guid expertId,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 10)
    {
        var result = await _profileService.GetPublicCompletedProjectsAsync(expertId, page, pageSize);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Expert completed projects retrieved successfully", HttpContext.TraceIdentifier));
    }
}

