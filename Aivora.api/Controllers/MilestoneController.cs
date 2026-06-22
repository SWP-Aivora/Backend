using Aivora.api.Extensions;
using Aivora.Services.MilestoneService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/milestones")]
[Authorize]
[EnableRateLimiting("General")]
public class MilestoneController : ControllerBase
{
    private readonly IService _milestoneService;
    private readonly Aivora.Services.DeliverableService.IService _deliverableService;
    private readonly Aivora.Services.DisputeService.IService _disputeService;

    public MilestoneController(
        IService milestoneService,
        Aivora.Services.DeliverableService.IService deliverableService,
        Aivora.Services.DisputeService.IService disputeService)
    {
        _milestoneService = milestoneService;
        _deliverableService = deliverableService;
        _disputeService = disputeService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMilestone(Guid id)
    {
        var userId = this.GetUserId();
        var result = await _milestoneService.GetMilestoneByIdAsync(userId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Milestone retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> UpdateMilestone(Guid id, [FromBody] Request.UpdateMilestoneRequest request)
    {
        var userId = this.GetUserId();
        var result = await _milestoneService.UpdateMilestoneAsync(userId, id, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Milestone updated successfully", HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}/fund")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> FundMilestone(Guid id)
    {
        var userId = this.GetUserId();
        var result = await _milestoneService.FundMilestoneAsync(userId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Milestone direct transfer recorded successfully", HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}/approve")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> ApproveMilestone(Guid id)
    {
        var userId = this.GetUserId();
        var result = await _milestoneService.ApproveMilestoneAsync(userId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Milestone approved and payment record completed", HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}/request-revision")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> RequestRevision(Guid id, [FromBody] string reason)
    {
        var userId = this.GetUserId();
        var result = await _milestoneService.RequestRevisionAsync(userId, id, reason);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Revision requested", HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/dispute")]
    public async Task<IActionResult> OpenDispute(Guid id, [FromBody] string reason)
    {
        var userId = this.GetUserId();
        var request = new Aivora.Services.DisputeService.Request.OpenDisputeRequest
        {
            MilestoneId = id,
            Reason = reason,
            Description = "Dispute opened via milestone shortcut."
        };
        var result = await _disputeService.OpenDisputeAsync(userId, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Dispute opened successfully", HttpContext.TraceIdentifier));
    }

    // --- Deliverable Endpoints ---

    [HttpGet("{id}/deliverables")]
    public async Task<IActionResult> GetDeliverables(Guid id)
    {
        var userId = this.GetUserId();
        var result = await _deliverableService.GetDeliverablesByMilestoneAsync(userId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Deliverables retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/deliverables")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> SubmitDeliverable(Guid id, [FromBody] Aivora.Services.DeliverableService.Request.SubmitDeliverableRequest request)
    {
        var expertId = this.GetUserId();
        var result = await _deliverableService.SubmitDeliverableAsync(expertId, id, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Deliverable submitted successfully", HttpContext.TraceIdentifier));
    }
}
