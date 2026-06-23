using Aivora.api.Extensions;
using Aivora.Services.Models;
using Aivora.Services.WalletService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/wallet")]
[Authorize]
[EnableRateLimiting("General")]
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

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactionHistory([FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest)
    {
        var userId = this.GetUserId();
        var result = await _walletService.GetTransactionHistoryAsync(userId, pageRequest);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Transaction history retrieved successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("deposit")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> Deposit([FromBody] Request.DepositRequest request)
    {
        var userId = this.GetUserId();
        var result = await _walletService.DepositAsync(userId, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Deposit processed successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("withdraw")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> Withdraw([FromBody] Request.WithdrawRequest request)
    {
        var userId = this.GetUserId();
        var result = await _walletService.WithdrawAsync(userId, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Withdrawal processed successfully", HttpContext.TraceIdentifier));
    }

    [HttpPost("transfer/{expertId}")]
    [Authorize(Policy = JwtExtensions.ClientPolicy)]
    public async Task<IActionResult> TransferToExpert(Guid expertId, [FromBody] Request.TransferRequest request)
    {
        request.RecipientId = expertId;
        var userId = this.GetUserId();
        var result = await _walletService.TransferToExpertAsync(userId, request);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Transfer to expert processed successfully", HttpContext.TraceIdentifier));
    }
}
