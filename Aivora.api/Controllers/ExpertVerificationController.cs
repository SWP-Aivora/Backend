using Aivora.api.Extensions;
using Aivora.Services.ExpertVerificationService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/expert/verifications")]
[Authorize(Policy = JwtExtensions.ExpertPolicy)]
[EnableRateLimiting("General")]
public class ExpertVerificationController : ControllerBase
{
    private readonly IService _verificationService;

    public ExpertVerificationController(IService verificationService)
    {
        _verificationService = verificationService;
    }

    [HttpPost]
    [EnableRateLimiting("AI")]
    public async Task<IActionResult> SubmitEvidence([FromForm] Request.SubmitEvidenceRequest request, CancellationToken cancellationToken)
    {
        var expertUserId = this.GetUserId();
        var result = await _verificationService.SubmitEvidenceAsync(expertUserId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponseFactory.SuccessResponse(result, "Verification evidence submitted", HttpContext.TraceIdentifier));
    }

    [HttpGet]
    public async Task<IActionResult> GetMyVerifications([FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest, [FromQuery] Guid? expertSkillId)
    {
        var expertUserId = this.GetUserId();
        var result = await _verificationService.GetMyVerificationsAsync(expertUserId, expertSkillId, pageRequest);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Verification history retrieved", HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/escalate")]
    public async Task<IActionResult> Escalate(Guid id)
    {
        var expertUserId = this.GetUserId();
        var result = await _verificationService.EscalateAsync(expertUserId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Verification escalated to admin review", HttpContext.TraceIdentifier));
    }
}
