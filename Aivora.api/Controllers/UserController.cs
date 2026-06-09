using Aivora.api.Extensions;
using Aivora.Services.Models;
using Aivora.Services.ProfileService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
[EnableRateLimiting("General")]
public class UserController : ControllerBase
{
    private readonly IService _profileService;

    public UserController(IService profileService)
    {
        _profileService = profileService;
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] Request.UpdateUserRequest request)
    {
        var userId = this.GetUserId();
        var result = await _profileService.UpdateUserAsync(userId, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Global user profile updated successfully", HttpContext.TraceIdentifier));
    }
}
