using Aivora.api.Extensions;
using Aivora.Repositories.Enums;
using Aivora.Services.Models;
using Aivora.Services.ProjectService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/projects")]
[Authorize]
[EnableRateLimiting("General")]
public class ProjectController : ControllerBase
{
    private readonly IService _projectService;
    private readonly Aivora.Services.MilestoneService.IService _milestoneService;

    public ProjectController(
        IService projectService,
        Aivora.Services.MilestoneService.IService milestoneService)
    {
        _projectService = projectService;
        _milestoneService = milestoneService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects([FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest, [FromQuery] ProjectStatus? status)
    {
        var userId = this.GetUserId();
        var role = this.GetUserRole();
        var result = await _projectService.GetProjectsAsync(userId, role, pageRequest, status);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Projects retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProject(Guid id)
    {
        var userId = this.GetUserId();
        var role = this.GetUserRole();
        var result = await _projectService.GetProjectByIdAsync(userId, id, role);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Project retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}/cancel")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> CancelProject(Guid id, [FromBody] string? reason)
    {
        var userId = this.GetUserId();
        var result = await _projectService.CancelProjectAsync(userId, id, reason);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Project cancelled successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/milestones")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> CreateMilestone(Guid id, [FromBody] Aivora.Services.MilestoneService.Request.CreateMilestoneRequest request)
    {
        var userId = this.GetUserId();
        var result = await _milestoneService.CreateMilestoneAsync(userId, id, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Milestone created successfully", HttpContext.TraceIdentifier));
    }
}
