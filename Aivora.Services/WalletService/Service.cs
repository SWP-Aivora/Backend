using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.WalletService;

public class WalletApplicationService : IService
{
    private readonly AivoraDbContext _dbContext;

    public WalletApplicationService(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Response.WalletResponse> GetWalletAsync(Guid userId)
    {
        var wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found.");

        return MapToResponse(wallet);
    }

    public async Task<Response.DepositResultResponse> DepositDemoAsync(Guid userId, Request.DepositDemoRequest request)
    {
        if (request.Amount <= 0) throw new ValidationException("Amount must be greater than 0.");

        var wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            decimal balanceBefore = wallet.AvailableBalance;
            wallet.AvailableBalance += request.Amount;

            var walletTx = new WalletTransaction
            {
                WalletId = wallet.Id,
                UserId = userId,
                Amount = request.Amount,
                Type = WalletTransactionType.DEMO_DEPOSIT,
                Direction = TransactionDirection.CREDIT,
                Description = request.Description ?? "Demo deposit",
                BalanceBefore = balanceBefore,
                BalanceAfter = wallet.AvailableBalance
            };

            _dbContext.WalletTransactions.Add(walletTx);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return new Response.DepositResultResponse
            {
                Wallet = MapToResponse(wallet),
                Transaction = MapToTxResponse(walletTx)
            };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Aivora.Services.Base.Response.PageResult<Response.TransactionResponse>> GetTransactionHistoryAsync(Guid userId, Aivora.Services.Base.Request.PageRequest pageRequest)
    {
        var query = _dbContext.WalletTransactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt);

        var totalItems = await query.CountAsync();
        var items = await query
            .Skip((pageRequest.PageIndex - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .ToListAsync();

        return new Aivora.Services.Base.Response.PageResult<Response.TransactionResponse>
        {
            Items = items.Select(MapToTxResponse).ToList(),
            TotalItems = totalItems,
            PageIndex = pageRequest.PageIndex,
            PageSize = pageRequest.PageSize
        };
    }

    private static Response.WalletResponse MapToResponse(Wallet w)
    {
        return new Response.WalletResponse
        {
            Id = w.Id,
            UserId = w.UserId,
            AvailableBalance = w.AvailableBalance,
            HeldBalance = w.HeldBalance,
            TotalEarned = w.TotalEarned,
            Currency = w.Currency,
            UpdatedAt = w.UpdatedAt
        };
    }

    private static Response.TransactionResponse MapToTxResponse(WalletTransaction t)
    {
        return new Response.TransactionResponse
        {
            Id = t.Id,
            WalletId = t.WalletId,
            PaymentId = t.PaymentId,
            Type = t.Type,
            Direction = t.Direction,
            Amount = t.Amount,
            BalanceBefore = t.BalanceBefore,
            BalanceAfter = t.BalanceAfter,
            Description = t.Description,
            CreatedAt = t.CreatedAt
        };
    }
}
