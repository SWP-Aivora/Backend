using Aivora.api.Extensions;
using Aivora.Repositories.Enums;
using Aivora.Services.Base;
using Aivora.Services.JobService;
using Aivora.Services.Models;
using Aivora.Services.VerificationService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

/// <summary>
/// Job controller handling job posting, searching, and management
/// </summary>
[ApiController]
[Route("api/v1/jobs")]
[EnableRateLimiting("General")]
public class JobController : ControllerBase
{
    private readonly IService _jobService;
    private readonly Aivora.Services.RecommendationService.IService _recommendationService;
    private readonly Aivora.Services.ProposalService.IService _proposalService;
    private readonly IVerificationService _verificationService;

    /// <summary>
    /// Initializes a new instance of the JobController
    /// </summary>
    /// <param name="jobService">Job service for job operations</param>
    /// <param name="recommendationService">Recommendation service for expert matching</param>
    /// <param name="proposalService">Proposal service for proposal management</param>
    /// <param name="verificationService">Verification service for expert validation</param>
    public JobController(
        IService jobService,
        Aivora.Services.RecommendationService.IService recommendationService,
        Aivora.Services.ProposalService.IService proposalService,
        IVerificationService verificationService)
    {
        _jobService = jobService;
        _recommendationService = recommendationService;
        _proposalService = proposalService;
        _verificationService = verificationService;
    }

    /// <summary>
    /// Get list of jobs with pagination and optional category filter
    /// </summary>
    /// <param name="pageRequest">Pagination parameters</param>
    /// <param name="categoryId">Optional category filter</param>
    /// <returns>Paginated list of jobs</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetJobs([FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest, [FromQuery] Guid? categoryId)
    {
        var result = await _jobService.GetJobsAsync(pageRequest, categoryId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Jobs retrieved successfully", HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Get job by ID
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <returns>Job details</returns>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetJob(Guid id)
    {
        var result = await _jobService.GetJobByIdAsync(id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Job retrieved successfully", HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Create a new job draft
    /// </summary>
    /// <param name="request">Job creation data</param>
    /// <returns>Created job information</returns>
    [HttpPost]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> CreateJob([FromBody] Aivora.Services.JobService.Request.CreateJobRequest request)
    {
        var clientId = this.GetUserId();
        var result = await _jobService.CreateJobAsync(clientId, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Job created successfully as draft", HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// Update an existing job
    /// </summary>
    /// <param name="id">Job ID</param>
    /// <param name="request">Job update data</param>
    /// <returns>Updated job information</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> UpdateJob(Guid id, [FromBody] Aivora.Services.JobService.Request.UpdateJobRequest request)
    {
        var clientId = this.GetUserId();
        var result = await _jobService.UpdateJobAsync(clientId, id, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Job updated successfully", HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> DeleteJob(Guid id)
    {
        var clientId = this.GetUserId();
        await _jobService.DeleteJobAsync(clientId, id);
        return Ok(ApiResponseFactory.SuccessResponse(null, "Job deleted successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/publish")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> PublishJob(Guid id)
    {
        var clientId = this.GetUserId();
        var result = await _jobService.PublishJobAsync(clientId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Job published successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/cancel")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> CancelJob(Guid id, [FromBody] string? reason)
    {
        var clientId = this.GetUserId();
        var result = await _jobService.CancelJobAsync(clientId, id, reason);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Job cancelled successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/recommendations/generate")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> GenerateRecommendations(Guid id)
    {
        var clientId = this.GetUserId();
        var result = await _recommendationService.GenerateRecommendationsAsync(clientId, id);
        return StatusCode(StatusCodes.Status201Created, ApiResponseFactory.SuccessResponse(result, "Expert recommendations generated", HttpContext.TraceIdentifier));
    }

    [HttpGet("{id}/recommendations")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> GetRecommendations(Guid id)
    {
        var clientId = this.GetUserId();
        var result = await _recommendationService.GetRecommendationsAsync(clientId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Expert recommendations retrieved", HttpContext.TraceIdentifier));
    }

    [HttpGet("my")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> GetMyJobs([FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest, [FromQuery] JobStatus? status)
    {
        var clientId = this.GetUserId();
        var result = await _jobService.GetMyJobsAsync(clientId, pageRequest, status);
        return Ok(ApiResponseFactory.SuccessResponse(result, "My jobs retrieved successfully", HttpContext.TraceIdentifier));
    }

    // --- Nested Proposal Endpoints ---

    [HttpGet("{id}/proposals")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> GetProposalsByJob(Guid id)
    {
        var userId = this.GetUserId();
        var result = await _proposalService.GetProposalsByJobIdAsync(userId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Proposals retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/proposals")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> SubmitProposal(Guid id, [FromBody] Aivora.Services.ProposalService.Request.CreateProposalRequest request)
    {
        request.JobId = id; // Ensure jobId from path is used
        var expertId = this.GetUserId();
        var result = await _proposalService.CreateProposalAsync(expertId, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Proposal submitted successfully", HttpContext.TraceIdentifier));
    }
}
