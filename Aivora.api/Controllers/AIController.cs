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
    public async Task<IActionResult> GenerateJobSuggestion([FromBody] Request.GenerateSuggestionRequest request, CancellationToken cancellationToken)
    {
        var clientId = this.GetUserId();
        var result = await _aiService.GenerateSuggestionAsync(clientId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponseFactory.SuccessResponse(result, "AI job suggestion generated", HttpContext.TraceIdentifier));
    }

    [HttpGet("job-assistant/{id}")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> GetJobSuggestion(Guid id, CancellationToken cancellationToken)
    {
        var clientId = this.GetUserId();
        var result = await _aiService.GetSuggestionAsync(clientId, id, cancellationToken);
        return Ok(ApiResponseFactory.SuccessResponse(result, "OK", HttpContext.TraceIdentifier));
    }

    [HttpPatch("job-assistant/{id}")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> PatchJobSuggestion(Guid id, [FromBody] Request.PatchSuggestionRequest request, CancellationToken cancellationToken)
    {
        var clientId = this.GetUserId();
        var result = await _aiService.PatchSuggestionAsync(clientId, id, request, cancellationToken);
        return Ok(ApiResponseFactory.SuccessResponse(result, "AI suggestion patched", HttpContext.TraceIdentifier));
    }

    [HttpPost("job-assistant/{id}/refine")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> RefineJobSuggestion(Guid id, [FromBody] Request.RefineSuggestionRequest request, CancellationToken cancellationToken)
    {
        var clientId = this.GetUserId();
        var result = await _aiService.RefineSuggestionAsync(clientId, id, request, cancellationToken);
        return Ok(ApiResponseFactory.SuccessResponse(result, "AI suggestion refined", HttpContext.TraceIdentifier));
    }

    [HttpPost("job-assistant/{id}/accept")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> AcceptJobSuggestion(Guid id, [FromBody] Request.AcceptSuggestionRequest request, CancellationToken cancellationToken)
    {
        var clientId = this.GetUserId();
        var result = await _aiService.AcceptSuggestionAsync(clientId, id, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponseFactory.SuccessResponse(result, "Job draft created from AI suggestion", HttpContext.TraceIdentifier));
    }

    [HttpPost("job-assistant/{id}/reject")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> RejectJobSuggestion(Guid id, [FromBody] Request.RejectSuggestionRequest request, CancellationToken cancellationToken)
    {
        var clientId = this.GetUserId();
        var result = await _aiService.RejectSuggestionAsync(clientId, id, request, cancellationToken);
        return Ok(ApiResponseFactory.SuccessResponse(result, "AI suggestion rejected", HttpContext.TraceIdentifier));
    }

    [HttpPost("service-generator")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> GenerateServiceDescription([FromBody] Request.GenerateServiceDescriptionRequest request, CancellationToken cancellationToken)
    {
        var expertId = this.GetUserId();
        var result = await _aiService.GenerateServiceDescriptionAsync(expertId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponseFactory.SuccessResponse(result, "Service description generated", HttpContext.TraceIdentifier));
    }
}
