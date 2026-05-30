using Aivora.api.Extensions;
using Aivora.Services.Models;
using Aivora.Services.WalletService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/wallet")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly IService _walletService;

    public WalletController(IService walletService)
    {
        _walletService = walletService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyWallet()
    {
        var userId = this.GetUserId();
        var result = await _walletService.GetWalletAsync(userId);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Wallet retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("deposit-demo")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> DepositDemo([FromBody] Request.DepositDemoRequest request)
    {
        var userId = this.GetUserId();
        var result = await _walletService.DepositDemoAsync(userId, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Demo deposit completed", HttpContext.TraceIdentifier));
    }
}
