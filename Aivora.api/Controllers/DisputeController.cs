using Aivora.api.Extensions;
using Aivora.Services.DisputeService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/disputes")]
[Authorize]
[EnableRateLimiting("General")]
public class DisputeController : ControllerBase
{
    private readonly IService _disputeService;

    public DisputeController(IService disputeService)
    {
        _disputeService = disputeService;
    }

    [HttpPost]
    public async Task<IActionResult> OpenDispute([FromBody] Request.OpenDisputeRequest request)
    {
        var userId = this.GetUserId();
        var result = await _disputeService.OpenDisputeAsync(userId, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Dispute opened successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet]
    public async Task<IActionResult> GetDisputes([FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest)
    {
        var userId = this.GetUserId();
        var role = this.GetUserRole().ToString();
        var result = await _disputeService.GetDisputesAsync(userId, role, pageRequest);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Disputes retrieved", HttpContext.TraceIdentifier));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDispute(Guid id)
    {
        var userId = this.GetUserId();
        var result = await _disputeService.GetDisputeByIdAsync(userId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Dispute details retrieved", HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/evidence")]
    public async Task<IActionResult> AddEvidence(Guid id, [FromBody] Request.AddEvidenceRequest request)
    {
        var userId = this.GetUserId();
        var result = await _disputeService.AddEvidenceAsync(userId, id, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Evidence added successfully", HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}/resolve")]
    [Authorize(Policy = JwtExtensions.AdminPolicy)]
    public async Task<IActionResult> ResolveDispute(Guid id, [FromBody] Request.ResolveDisputeRequest request)
    {
        var adminId = this.GetUserId();
        var result = await _disputeService.ResolveDisputeAsync(adminId, id, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Dispute resolved successfully", HttpContext.TraceIdentifier));
    }
}
