using Aivora.api.Extensions;
using Aivora.Services.Models;
using Aivora.Services.ProposalService;
using Aivora.Services.HiringWorkflowService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/proposals")]
[Authorize]
public class ProposalController : ControllerBase
{
    private readonly Aivora.Services.ProposalService.IService _proposalService;
    private readonly Aivora.Services.HiringWorkflowService.IService _hiringService;

    public ProposalController(
        Aivora.Services.ProposalService.IService proposalService,
        Aivora.Services.HiringWorkflowService.IService hiringService)
    {
        _proposalService = proposalService;
        _hiringService = hiringService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProposal(Guid id)
    {
        var result = await _proposalService.GetProposalByIdAsync(id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Proposal retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> SubmitProposal([FromBody] Aivora.Services.ProposalService.Request.CreateProposalRequest request)
    {
        var expertId = this.GetUserId();
        var result = await _proposalService.CreateProposalAsync(expertId, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Proposal submitted successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("job/{jobId}")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> GetProposalsByJob(Guid jobId)
    {
        var userId = this.GetUserId();
        var result = await _proposalService.GetProposalsByJobIdAsync(userId, jobId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Proposals retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpGet("me")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> GetMyProposals()
    {
        var expertId = this.GetUserId();
        var result = await _proposalService.GetExpertProposalsAsync(expertId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "My proposals retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}/accept")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> AcceptProposal(Guid id)
    {
        var userId = this.GetUserId();
        var result = await _hiringService.AcceptProposalAsync(userId, id);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Proposal accepted and project initialized", HttpContext.TraceIdentifier));
    }
}
