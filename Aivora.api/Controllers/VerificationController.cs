using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.VerificationService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Aivora.Services.Exceptions;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class VerificationController : ControllerBase
{
    private readonly IVerificationService _verificationService;

    public VerificationController(IVerificationService verificationService)
    {
        _verificationService = verificationService;
    }

    // Expert endpoints
    [HttpPost("start")]
    public async Task<IActionResult> StartVerification()
    {
        var expertId = GetExpertId();
        var verification = await _verificationService.StartVerificationAsync(expertId);

        return Ok(new
        {
            Message = "Verification process started",
            Verification = new
            {
                verification.Id,
                verification.Status,
                verification.CurrentStage,
                verification.ProcessingStatus,
                verification.TotalScore,
                verification.IsPassed,
                verification.RetryCount,
                verification.MaxRetries
            }
        });
    }

    [HttpGet("progress")]
    public async Task<IActionResult> GetVerificationProgress()
    {
        var expertId = GetExpertId();
        var verification = await _verificationService.GetVerificationAsync(expertId);

        if (verification == null)
            return NotFound(new { Message = "No verification found" });

        // Calculate progress percentage
        var progress = CalculateProgress(verification);

        return Ok(new
        {
            Verification = new
            {
                verification.Id,
                verification.Status,
                verification.CurrentStage,
                verification.ProcessingStatus,
                verification.TotalScore,
                verification.ProfileScore,
                verification.SkillsScore,
                verification.CertificatesScore,
                verification.Feedback,
                verification.FailureReason,
                verification.RetryCount
            },
            Progress = progress
        });
    }

    [HttpPost("appeal")]
    public async Task<IActionResult> SubmitAppeal([FromBody] SubmitAppealRequest request)
    {
        var expertId = GetExpertId();
        await _verificationService.SubmitAppealAsync(expertId, request.Reason);

        return Ok(new { Message = "Appeal submitted successfully" });
    }

    // Admin endpoints
    [HttpGet("admin/pending")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> GetPendingVerifications()
    {
        var verifications = await _verificationService.GetPendingVerificationsAsync();
        var response = verifications.Select(v => new
        {
            v.Id,
            v.ExpertId,
            v.Expert.User.FullName,
            v.Status,
            v.TotalScore,
            v.CurrentStage,
            v.ProcessingStatus,
            v.CreatedAt,
            v.LastProcessedAt
        });

        return Ok(response);
    }

    [HttpGet("admin/failed")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> GetFailedVerifications()
    {
        var verifications = await _verificationService.GetFailedVerificationsAsync();
        var response = verifications.Select(v => new
        {
            v.Id,
            v.ExpertId,
            v.Expert.User.FullName,
            v.Status,
            v.TotalScore,
            v.FailureReason,
            v.RetryCount,
            v.UpdatedAt
        });

        return Ok(response);
    }

    [HttpPost("admin/{expertId}/approve")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> AdminApprove(Guid expertId)
    {
        var verification = await _verificationService.AdminApproveAsync(expertId);

        return Ok(new
        {
            Message = "Expert verification approved",
            Verification = new
            {
                verification.Id,
                verification.Status,
                verification.TotalScore,
                verification.Feedback
            }
        });
    }

    [HttpPost("admin/{expertId}/reject")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> AdminReject(Guid expertId, [FromBody] AdminVerificationActionRequest request)
    {
        var verification = await _verificationService.AdminRejectAsync(expertId, request.Reason ?? "Rejected by admin");

        return Ok(new
        {
            Message = "Expert verification rejected",
            Verification = new
            {
                verification.Id,
                verification.Status,
                verification.TotalScore,
                verification.Feedback
            }
        });
    }

    [HttpPost("admin/{appealId}/review")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> ReviewAppeal(Guid appealId, [FromBody] ReviewAppealRequest request)
    {
        var verification = await _verificationService.ReviewAppealAsync(
            appealId,
            request.Approved,
            request.AdminFeedback
        );

        return Ok(new
        {
            Message = "Appeal reviewed",
            Verification = new
            {
                verification.Id,
                verification.Status,
                verification.Feedback
            }
        });
    }

    [HttpPost("admin/{expertId}/retry")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> RetryVerification(Guid expertId)
    {
        var verification = await _verificationService.RetryFailedVerificationAsync(expertId);

        return Ok(new
        {
            Message = "Verification retry initiated",
            Verification = new
            {
                verification.Id,
                verification.Status,
                verification.TotalScore,
                verification.RetryCount
            }
        });
    }

    // Analytics endpoint
    [HttpGet("admin/analytics")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> GetAnalytics()
    {
        var analytics = await _verificationService.GetAnalyticsAsync();

        return Ok(new
        {
            Analytics = new
            {
                analytics.TotalExperts,
                analytics.PendingVerifications,
                analytics.PassedVerifications,
                analytics.FailedVerifications,
                analytics.PassRate,
                analytics.AverageProcessingTimeMinutes,
                analytics.AverageRetryCount,
                analytics.FailureReasons
            }
        });
    }

    // Certificate endpoints
    // Certificate endpoints to be implemented later

    // Public endpoints
    [HttpGet("experts/{expertId}/status")]
    [Authorize(Roles = "CLIENT,ADMIN")]
    public async Task<IActionResult> GetExpertVerificationStatus(Guid expertId)
    {
        var verification = await _verificationService.GetVerificationAsync(expertId);

        if (verification == null)
            return NotFound(new { Message = "Expert verification not found" });

        return Ok(new
        {
            ExpertId = expertId,
            Status = verification.Status,
            IsPassed = verification.IsPassed,
            TotalScore = verification.TotalScore,
            VerifiedAt = verification.Expert?.VerifiedAt
        });
    }

    private Guid GetExpertId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedException("User not found");

        return Guid.Parse(userId);
    }

    private IVerificationService GetVerificationService() => _verificationService;

    private int CalculateProgress(ExpertVerification verification)
    {
        if (verification.Status == VerificationStatus.VERIFIED)
            return 100;

        if (verification.Status == VerificationStatus.REJECTED)
            return 0;

        // Calculate based on stages completed
        var progress = 0;
        if (verification.IsProfileProcessed) progress += 33;
        if (verification.IsSkillsProcessed) progress += 33;
        if (verification.IsCertificatesProcessed) progress += 34;

        return Math.Clamp(progress, 0, 99);
    }
}