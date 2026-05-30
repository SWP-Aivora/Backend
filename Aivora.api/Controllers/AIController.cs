using Aivora.api.Extensions;
using Aivora.Services.AIJobAssistantService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/ai")]
[Authorize]
public class AIController : ControllerBase
{
    private readonly IService _aiService;

    public AIController(IService aiService)
    {
        _aiService = aiService;
    }

    [HttpPost("job-assistant")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> GenerateJobSuggestion([FromBody] Request.GenerateSuggestionRequest request)
    {
        var clientId = this.GetUserId();
        var result = await _aiService.GenerateSuggestionAsync(clientId, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "AI job suggestion generated", HttpContext.TraceIdentifier));
    }

    [HttpPost("job-assistant/{id}/accept")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> AcceptJobSuggestion(Guid id, [FromBody] Request.AcceptSuggestionRequest request)
    {
        var clientId = this.GetUserId();
        var result = await _aiService.AcceptSuggestionAsync(clientId, id, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Job draft created from AI suggestion", HttpContext.TraceIdentifier));
    }

    [HttpPost("job-assistant/{id}/reject")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> RejectJobSuggestion(Guid id, [FromBody] Request.RejectSuggestionRequest request)
    {
        var clientId = this.GetUserId();
        await _aiService.RejectSuggestionAsync(clientId, id, request);
        return Ok(ApiResponseFactory.SuccessResponse(null, "AI suggestion rejected", HttpContext.TraceIdentifier));
    }
}
