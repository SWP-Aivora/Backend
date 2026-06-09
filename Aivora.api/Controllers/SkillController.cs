using Aivora.api.Extensions;
using Aivora.Services.SkillService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/skills")]
[EnableRateLimiting("General")]
public class SkillController : ControllerBase
{
    private readonly IService _skillService;

    public SkillController(IService skillService)
    {
        _skillService = skillService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSkills([FromQuery] Guid? categoryId)
    {
        var result = await _skillService.GetSkillsAsync(categoryId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Skills retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSkill(Guid id)
    {
        var result = await _skillService.GetSkillByIdAsync(id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Skill retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<IActionResult> CreateSkill([FromBody] Request.CreateSkillRequest request)
    {
        var result = await _skillService.CreateSkillAsync(request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Skill created successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("expert/me")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> AddExpertSkill([FromBody] Request.AddExpertSkillRequest request)
    {
        var userId = this.GetUserId();
        var result = await _skillService.AddExpertSkillAsync(userId, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Skill added to expert profile", HttpContext.TraceIdentifier));
    }

    [HttpDelete("expert/me/{skillId}")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> RemoveExpertSkill(Guid skillId)
    {
        var userId = this.GetUserId();
        await _skillService.RemoveExpertSkillAsync(userId, skillId);
        return Ok(ApiResponseFactory.SuccessResponse(null, "Skill removed from expert profile", HttpContext.TraceIdentifier));
    }
}
