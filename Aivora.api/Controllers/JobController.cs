using Aivora.api.Extensions;
using Aivora.Services.Base;
using Aivora.Services.JobService;
using Aivora.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/jobs")]
public class JobController : ControllerBase
{
    private readonly IService _jobService;

    public JobController(IService jobService)
    {
        _jobService = jobService;
    }

    [HttpGet]
    public async Task<IActionResult> GetJobs([FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest, [FromQuery] Guid? categoryId)
    {
        var result = await _jobService.GetJobsAsync(pageRequest, categoryId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Jobs retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetJob(Guid id)
    {
        var result = await _jobService.GetJobByIdAsync(id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Job retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> CreateJob([FromBody] Aivora.Services.JobService.Request.CreateJobRequest request)
    {
        var clientId = this.GetUserId();
        var result = await _jobService.CreateJobAsync(clientId, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Job created successfully as draft", HttpContext.TraceIdentifier));
    }

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
}
