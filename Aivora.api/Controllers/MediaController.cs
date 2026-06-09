using Aivora.Services.MediaService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/media")]
[Authorize]
[EnableRateLimiting("General")]
public class MediaController : ControllerBase
{
    private readonly IService _mediaService;

    public MediaController(IService mediaService)
    {
        _mediaService = mediaService;
    }

    [HttpPost("upload-image")]
    public async Task<IActionResult> UploadImage(IFormFile file, [FromQuery] string folder = "general")
    {
        var result = await _mediaService.UploadImageAsync(file, folder);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Image uploaded successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("upload-file")]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromQuery] string folder = "general")
    {
        var result = await _mediaService.UploadFileAsync(file, folder);
        return Ok(ApiResponseFactory.SuccessResponse(result, "File uploaded successfully", HttpContext.TraceIdentifier));
    }

    [HttpDelete("{publicId}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> DeleteMedia(string publicId)
    {
        await _mediaService.DeleteMediaAsync(publicId);
        return Ok(ApiResponseFactory.SuccessResponse(null, "Media deleted successfully", HttpContext.TraceIdentifier));
    }
}
