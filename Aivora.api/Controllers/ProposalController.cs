using Aivora.api.Extensions;
using Aivora.Services.Models;
using Aivora.Services.ProposalService;
using Aivora.Services.HiringService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/proposals")]
[Authorize]
public class ProposalController : ControllerBase
{
    private readonly Aivora.Services.ProposalService.IService _proposalService;
    private readonly IHiringService _hiringService;

    public ProposalController(
        Aivora.Services.ProposalService.IService proposalService,
        IHiringService hiringService)
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

    [HttpGet("me")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> GetMyProposals()
    {
        var expertId = this.GetUserId();
        var result = await _proposalService.GetExpertProposalsAsync(expertId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "My proposals retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}/shortlist")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> ShortlistProposal(Guid id)
    {
        var userId = this.GetUserId();
        await _hiringService.ShortlistProposalAsync(userId, id);
        return Ok(ApiResponseFactory.SuccessResponse(true, "Proposal shortlisted", HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}/reject")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> RejectProposal(Guid id)
    {
        var userId = this.GetUserId();
        await _hiringService.RejectProposalAsync(userId, id);
        return Ok(ApiResponseFactory.SuccessResponse(true, "Proposal rejected", HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}/withdraw")]
    [Authorize(Policy = JwtExtensions.ExpertPolicy)]
    public async Task<IActionResult> WithdrawProposal(Guid id)
    {
        var userId = this.GetUserId();
        await _hiringService.WithdrawProposalAsync(userId, id);
        return Ok(ApiResponseFactory.SuccessResponse(true, "Proposal withdrawn", HttpContext.TraceIdentifier));
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
