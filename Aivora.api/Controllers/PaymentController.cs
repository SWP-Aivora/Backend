using Aivora.api.Extensions;
using Aivora.Services.Models;
using Aivora.Services.WalletService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aivora.api.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly IService _walletService;

    public PaymentController(IService walletService)
    {
        _walletService = walletService;
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] Aivora.Services.Base.Request.PageRequest pageRequest)
    {
        var userId = this.GetUserId();
        var result = await _walletService.GetTransactionHistoryAsync(userId, pageRequest);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Transaction history retrieved successfully", HttpContext.TraceIdentifier));
    }
}
