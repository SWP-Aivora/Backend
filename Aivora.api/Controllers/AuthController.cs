using Aivora.Services.IdentityService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Mvc;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IService _identityService;

    public AuthController(IService identityService)
    {
        _identityService = identityService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] Request.LoginRequest request)
    {
        var result = await _identityService.LoginAsync(request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Login successful", HttpContext.TraceIdentifier));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] Request.RegisterRequest request)
    {
        var result = await _identityService.RegisterAsync(request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Registration successful", HttpContext.TraceIdentifier));
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] Request.RefreshTokenRequest request)
    {
        var result = await _identityService.RefreshTokenAsync(request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Token refreshed successfully", HttpContext.TraceIdentifier));
    }
}
