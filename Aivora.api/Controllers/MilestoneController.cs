using Aivora.api.Extensions;
using Aivora.Services.MilestoneService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/milestones")]
[Authorize]
public class MilestoneController : ControllerBase
{
    private readonly IService _milestoneService;

    public MilestoneController(IService milestoneService)
    {
        _milestoneService = milestoneService;
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
}
