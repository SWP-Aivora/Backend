using Aivora.api.Extensions;
using Aivora.Services.Models;
using Aivora.Services.ServiceOfferService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
[EnableRateLimiting("General")]
public class ServiceOffersController : ControllerBase
{
    private readonly IService _serviceOfferService;

    public ServiceOffersController(IService serviceOfferService)
    {
        _serviceOfferService = serviceOfferService;
    }

    [HttpPost("service-requests/{id}/offers")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> CreateServiceOffer(Guid id, [FromBody] Request.CreateServiceOfferRequest request)
    {
        var expertId = this.GetUserId();
        var result = await _serviceOfferService.CreateOfferAsync(expertId, id, request);
        return StatusCode(201, ApiResponseFactory.SuccessResponse(result, "Service offer created successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("service-offers/{id}/accept")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> AcceptServiceOffer(Guid id)
    {
        var clientId = this.GetUserId();
        var result = await _serviceOfferService.AcceptOfferAsync(clientId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Service offer accepted", HttpContext.TraceIdentifier));
    }

    [HttpGet("service-requests/{id}/offer")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> GetServiceRequestOffer(Guid id)
    {
        var clientId = this.GetUserId();
        var result = await _serviceOfferService.GetOfferForServiceRequestAsync(clientId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Service offer retrieved", HttpContext.TraceIdentifier));
    }
}
