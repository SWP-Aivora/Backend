using Aivora.api.Extensions;
using Aivora.Repositories.Enums;
using Aivora.Services.Models;
using Aivora.Services.ServiceRequestService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
[EnableRateLimiting("General")]
public class ServiceRequestsController : ControllerBase
{
    private readonly IService _serviceRequestService;

    public ServiceRequestsController(IService serviceRequestService)
    {
        _serviceRequestService = serviceRequestService;
    }

    [HttpPost("services/{id}/requests")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> CreateServiceRequest(Guid id, [FromBody] Request.CreateServiceRequestRequest request)
    {
        var clientId = this.GetUserId();
        var result = await _serviceRequestService.CreateRequestAsync(clientId, id, request);
        return StatusCode(201, ApiResponseFactory.SuccessResponse(result, "Service request created successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("services/{id}/requests")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> GetServiceRequests(Guid id)
    {
        var expertId = this.GetUserId();
        var result = await _serviceRequestService.GetRequestsByServiceAsync(expertId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Service requests retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("experts/me/service-requests")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> GetMyServiceRequests([FromQuery] ServiceRequestStatus? status)
    {
        var expertId = this.GetUserId();
        var result = await _serviceRequestService.GetMyRequestsForExpertAsync(expertId, status);
        return Ok(ApiResponseFactory.SuccessResponse(result, "My service requests retrieved successfully", HttpContext.TraceIdentifier));
    }
}
