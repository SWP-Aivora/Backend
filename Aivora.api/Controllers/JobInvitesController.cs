using Aivora.api.Extensions;
using Aivora.Repositories.Enums;
using Aivora.Services.JobInviteService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
[EnableRateLimiting("General")]
public class JobInvitesController : ControllerBase
{
    private readonly IService _jobInviteService;

    public JobInvitesController(IService jobInviteService)
    {
        _jobInviteService = jobInviteService;
    }

    [HttpPost("jobs/{id}/invites")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> CreateInvite(Guid id, [FromBody] Request.CreateJobInviteRequest request)
    {
        var clientId = this.GetUserId();
        var result = await _jobInviteService.CreateInviteAsync(clientId, id, request);
        return StatusCode(201, ApiResponseFactory.SuccessResponse(result, "Job invite created successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("jobs/{id}/invites")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> GetInvitesByJob(Guid id)
    {
        var clientId = this.GetUserId();
        var result = await _jobInviteService.GetInvitesByJobAsync(clientId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Job invites retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("experts/me/invites")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> GetMyInvites([FromQuery] JobInviteStatus? status)
    {
        var expertId = this.GetUserId();
        var result = await _jobInviteService.GetMyInvitesForExpertAsync(expertId, status);
        return Ok(ApiResponseFactory.SuccessResponse(result, "My job invites retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("invites/{id}/accept")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> AcceptInvite(Guid id)
    {
        var expertId = this.GetUserId();
        var result = await _jobInviteService.AcceptInviteAsync(expertId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Job invite accepted", HttpContext.TraceIdentifier));
    }

    [HttpPost("invites/{id}/decline")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> DeclineInvite(Guid id)
    {
        var expertId = this.GetUserId();
        var result = await _jobInviteService.DeclineInviteAsync(expertId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Job invite declined", HttpContext.TraceIdentifier));
    }
}
