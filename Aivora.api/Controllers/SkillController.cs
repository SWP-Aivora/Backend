using Aivora.Services.SkillService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Mvc;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/skills")]
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
}
