using Aivora.api.Extensions;
using Aivora.Services.Models;
using Aivora.Services.ServiceCatalogService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/services")]
[Authorize]
[EnableRateLimiting("General")]
public class ServicesController : ControllerBase
{
    private readonly IService _serviceCatalogService;

    public ServicesController(IService serviceCatalogService)
    {
        _serviceCatalogService = serviceCatalogService;
    }

    [HttpPost]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> CreateService([FromBody] Request.CreateServiceRequest request)
    {
        var expertId = this.GetUserId();
        var result = await _serviceCatalogService.CreateServiceAsync(expertId, request);
        return StatusCode(201, ApiResponseFactory.SuccessResponse(result, "Service created successfully", HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> UpdateService(Guid id, [FromBody] Request.UpdateServiceRequest request)
    {
        var expertId = this.GetUserId();
        var result = await _serviceCatalogService.UpdateServiceAsync(expertId, id, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Service updated successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/publish")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> PublishService(Guid id)
    {
        var expertId = this.GetUserId();
        var result = await _serviceCatalogService.PublishServiceAsync(expertId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Service published successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/unpublish")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> UnpublishService(Guid id)
    {
        var expertId = this.GetUserId();
        var result = await _serviceCatalogService.UnpublishServiceAsync(expertId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Service unpublished successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("mine")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> GetMyServices()
    {
        var expertId = this.GetUserId();
        var result = await _serviceCatalogService.GetMyServicesAsync(expertId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "My services retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetService(Guid id)
    {
        Guid? viewerId = User.Identity?.IsAuthenticated == true ? this.GetUserId() : null;
        var result = await _serviceCatalogService.GetServiceByIdAsync(id, viewerId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Service retrieved successfully", HttpContext.TraceIdentifier));
    }
}
