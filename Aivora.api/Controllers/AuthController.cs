using Aivora.api.Extensions;
using Aivora.Services.IdentityService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

/// <summary>
/// Authentication controller handling user login, registration, and token refresh
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("Strict")]
public class AuthController : ControllerBase
{
    private readonly IService _identityService;

    /// <summary>
    /// Initializes a new instance of the AuthController
    /// </summary>
    /// <param name="identityService">Identity service for authentication operations</param>
    public AuthController(IService identityService)
    {
        _identityService = identityService;
    }

    /// <summary>
    /// User login endpoint
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>Access and refresh tokens</returns>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] Request.LoginRequest request)
    {
        var result = await _identityService.LoginAsync(request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Login successful", HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// User registration endpoint
    /// </summary>
    /// <param name="request">Registration data</param>
    /// <returns>Created user information</returns>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] Request.RegisterRequest request)
    {
        var result = await _identityService.RegisterAsync(request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Registration successful", HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Refresh access token endpoint
    /// </summary>
    /// <param name="request">Refresh token</param>
    /// <returns>New access and refresh tokens</returns>
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] Request.RefreshTokenRequest request)
    {
        var result = await _identityService.RefreshTokenAsync(request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Token refreshed successfully", HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Get current user profile
    /// </summary>
    /// <returns>Current user information</returns>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var userId = this.GetUserId();
        var result = await _identityService.GetCurrentUserAsync(userId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "User data retrieved successfully", HttpContext.TraceIdentifier));
    }
}
