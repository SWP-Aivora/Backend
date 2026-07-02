using Aivora.api.Extensions;
using Aivora.Services.Models;
using Aivora.Services.ReviewService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1")]
[EnableRateLimiting("General")]
public class ReviewController : ControllerBase
{
    private readonly IService _reviewService;

    public ReviewController(IService reviewService)
    {
        _reviewService = reviewService;
    }

    [HttpPost("reviews")]
    [Authorize]
    public async Task<IActionResult> CreateReview([FromBody] Request.CreateReviewRequest request)
    {
        var userId = this.GetUserId();
        var result = await _reviewService.CreateReviewAsync(userId, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Review created successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("users/{userId}/reviews")]
    public async Task<IActionResult> GetUserReviews(Guid userId, [FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest)
    {
        var result = await _reviewService.GetUserReviewsAsync(userId, pageRequest);
        return Ok(ApiResponseFactory.SuccessResponse(result, "User reviews retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("projects/{projectId}/reviews")]
    public async Task<IActionResult> GetProjectReviews(Guid projectId, [FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest)
    {
        var result = await _reviewService.GetProjectReviewsAsync(projectId, pageRequest);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Project reviews retrieved successfully", HttpContext.TraceIdentifier));
    }
}
