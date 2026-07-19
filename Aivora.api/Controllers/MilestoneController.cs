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
        return Ok(ApiResponseFactory.SuccessResponse(result, "Milestone funded successfully", HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}/approve")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> ApproveMilestone(Guid id)
    {
        var userId = this.GetUserId();
        var result = await _milestoneService.ApproveMilestoneAsync(userId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Milestone approved and payment released", HttpContext.TraceIdentifier));
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
            Reason = reason
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

    // --- Milestone Step Endpoints ---

    [HttpGet("{id}/steps")]
    public async Task<IActionResult> GetMilestoneSteps(Guid id)
    {
        var userId = this.GetUserId();
        var result = await _milestoneService.GetMilestoneStepsAsync(userId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Milestone steps retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/steps")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> AddMilestoneStep(Guid id, [FromBody] Request.CreateMilestoneStepRequest request)
    {
        var userId = this.GetUserId();
        var result = await _milestoneService.AddMilestoneStepAsync(userId, id, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Milestone step added successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/steps/suggest")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    [EnableRateLimiting("AI")]
    public async Task<IActionResult> SuggestMilestoneSteps(Guid id, CancellationToken cancellationToken)
    {
        var userId = this.GetUserId();
        var result = await _milestoneService.SuggestMilestoneStepsAsync(userId, id, cancellationToken);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Milestone step suggestions generated", HttpContext.TraceIdentifier));
    }

    [HttpPut("~/api/v1/steps/{id}")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> UpdateMilestoneStep(Guid id, [FromBody] Request.UpdateMilestoneStepRequest request)
    {
        var userId = this.GetUserId();
        var result = await _milestoneService.UpdateMilestoneStepAsync(userId, id, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Milestone step updated successfully", HttpContext.TraceIdentifier));
    }

    [HttpDelete("~/api/v1/steps/{id}")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> DeleteMilestoneStep(Guid id)
    {
        var userId = this.GetUserId();
        await _milestoneService.DeleteMilestoneStepAsync(userId, id);
        return Ok(ApiResponseFactory.SuccessResponse(null, "Milestone step deleted successfully", HttpContext.TraceIdentifier));
    }

    // Both roles may call this at the policy level: the service itself narrows further,
    // only allowing the client to unblock (BLOCKED -> IN_PROGRESS) and the expert for
    // every other transition.
    [HttpPut("~/api/v1/steps/{id}/status")]
    [Authorize(Policy = JwtExtensions.ClientOrExpertPolicy)]
    public async Task<IActionResult> UpdateStepStatus(Guid id, [FromBody] Request.UpdateStepStatusRequest request)
    {
        var userId = this.GetUserId();
        var result = await _milestoneService.UpdateStepStatusAsync(userId, id, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Milestone step status updated successfully", HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}/steps/reorder")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> ReorderMilestoneSteps(Guid id, [FromBody] List<Guid> stepIds)
    {
        var userId = this.GetUserId();
        await _milestoneService.ReorderMilestoneStepsAsync(userId, id, stepIds);
        return Ok(ApiResponseFactory.SuccessResponse(null, "Milestone steps reordered successfully", HttpContext.TraceIdentifier));
    }
}
