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
    /// Helper method to set HttpOnly cookies for tokens
    /// </summary>
    private void SetTokenCookies(string accessToken, string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // Ensure this is true in production (requires HTTPS)
            SameSite = SameSiteMode.None,
            Expires = DateTime.UtcNow.AddDays(7) // Match refresh token expiration
        };

        Response.Cookies.Append("accessToken", accessToken, cookieOptions);
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
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
        SetTokenCookies(result.AccessToken, result.RefreshToken);
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
        SetTokenCookies(result.AccessToken, result.RefreshToken);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Registration successful", HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Refresh access token endpoint
    /// </summary>
    /// <param name="request">Refresh token</param>
    /// <returns>New access and refresh tokens</returns>
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] Request.RefreshTokenRequest? request)
    {
        var token = request?.RefreshToken ?? Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(token))
        {
            return Unauthorized(ApiResponseFactory.ErrorResponse("No refresh token provided", HttpContext.TraceIdentifier));
        }

        var refreshRequest = new Request.RefreshTokenRequest { RefreshToken = token };
        var result = await _identityService.RefreshTokenAsync(refreshRequest);
        SetTokenCookies(result.AccessToken, result.RefreshToken);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Token refreshed successfully", HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Logout endpoint to clear cookies
    /// </summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("accessToken", new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.None });
        Response.Cookies.Delete("refreshToken", new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.None });
        return Ok(ApiResponseFactory.SuccessResponse(null, "Logged out successfully", HttpContext.TraceIdentifier));
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
