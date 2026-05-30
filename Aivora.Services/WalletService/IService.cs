using Aivora.Services.Base;

namespace Aivora.Services.WalletService;

public interface IService
{
    Task<Response.WalletResponse> GetWalletAsync(Guid userId);
    Task<Response.DepositResultResponse> DepositDemoAsync(Guid userId, Request.DepositDemoRequest request);
    Task<Aivora.Services.Base.Response.PageResult<Response.TransactionResponse>> GetTransactionHistoryAsync(Guid userId, Aivora.Services.Base.Request.PageRequest pageRequest);
}
