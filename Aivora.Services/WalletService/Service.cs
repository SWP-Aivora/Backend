using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.WalletService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly IVNPayService _vnPayService;

    public Service(AivoraDbContext dbContext, IVNPayService vnPayService)
    {
        _dbContext = dbContext;
        _vnPayService = vnPayService;
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
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new ValidationException($"Transaction failed: {ex.Message}");
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

    public async Task<Response.DepositResultResponse> DepositAsync(Guid userId, Request.DepositRequest request)
    {
        if (request.Amount <= 0) throw new ValidationException("Amount must be greater than 0.");

        var wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found.");

        // In production, this would integrate with payment gateway
        // For now, we'll process it as a successful deposit
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
                Type = WalletTransactionType.DEPOSIT,
                Direction = TransactionDirection.CREDIT,
                Description = request.Description ?? $"Deposit via {request.PaymentMethod}",
                PaymentId = null,
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
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new ValidationException($"Transaction failed: {ex.Message}");
        }
    }

    public async Task<Response.VnPayDepositResponse> DepositViaVNPayAsync(Guid userId, Request.VnPayDepositRequest request)
    {
        if (request.Amount <= 0) throw new ValidationException("Amount must be greater than 0.");

        var wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found.");

        var orderInfo = $"Nap tien Aivora - User {userId}";
        var result = _vnPayService.CreatePaymentUrl(userId, request.Amount, orderInfo, wallet);

        return new Response.VnPayDepositResponse
        {
            PaymentUrl = result.PaymentUrl,
            TxnRef = result.TxnRef
        };
    }

    public async Task<Response.DepositResultResponse> WithdrawAsync(Guid userId, Request.WithdrawRequest request)
    {
        if (request.Amount <= 0) throw new ValidationException("Amount must be greater than 0.");

        var wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found.");

        if (wallet.AvailableBalance < request.Amount)
            throw new ValidationException("Insufficient balance for withdrawal.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // Get fresh wallet within transaction
            var currentWallet = await _dbContext.Wallets.FindAsync(wallet.Id);
            decimal balanceBefore = currentWallet.AvailableBalance;
            currentWallet.AvailableBalance -= request.Amount;

            var walletTx = new WalletTransaction
            {
                WalletId = wallet.Id,
                UserId = userId,
                Amount = request.Amount,
                Type = WalletTransactionType.WITHDRAWAL,
                Direction = TransactionDirection.DEBIT,
                Description = request.Description ?? $"Withdrawal via {request.PaymentMethod}",
                PaymentId = null,
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
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new ValidationException($"Transaction failed: {ex.Message}");
        }
    }

    public async Task<Response.DepositResultResponse> TransferToExpertAsync(Guid userId, Request.TransferRequest request)
    {
        if (request.Amount <= 0) throw new ValidationException("Amount must be greater than 0.");

        var wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet == null) throw new NotFoundException("Wallet not found.");

        if (wallet.AvailableBalance < request.Amount)
            throw new ValidationException("Insufficient balance for transfer.");

        var expertWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == request.RecipientId);
        if (expertWallet == null) throw new NotFoundException("Expert wallet not found.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // Check balances again within transaction to prevent race conditions
            var currentWallet = await _dbContext.Wallets.FindAsync(wallet.Id);
            var currentExpertWallet = await _dbContext.Wallets.FindAsync(expertWallet.Id);

            if (currentWallet.AvailableBalance < request.Amount)
                throw new ValidationException("Insufficient balance for transfer.");

            // Deduct from client
            decimal clientBalanceBefore = currentWallet.AvailableBalance;
            currentWallet.AvailableBalance -= request.Amount;

            // Add to expert (held until project completion)
            decimal expertBalanceBefore = currentExpertWallet.AvailableBalance;
            currentExpertWallet.AvailableBalance += request.Amount;

            // Create transactions
            var clientTx = new WalletTransaction
            {
                WalletId = wallet.Id,
                UserId = userId,
                Amount = request.Amount,
                Type = WalletTransactionType.TRANSFER,
                Direction = TransactionDirection.DEBIT,
                Description = request.Description ?? $"Transfer to expert",
                PaymentId = null,
                BalanceBefore = clientBalanceBefore,
                BalanceAfter = wallet.AvailableBalance
            };

            var expertTx = new WalletTransaction
            {
                WalletId = expertWallet.Id,
                UserId = request.RecipientId,
                Amount = request.Amount,
                Type = WalletTransactionType.TRANSFER,
                Direction = TransactionDirection.CREDIT,
                Description = request.Description ?? $"Transfer from client",
                PaymentId = clientTx.PaymentId,
                BalanceBefore = expertBalanceBefore,
                BalanceAfter = expertWallet.AvailableBalance
            };

            _dbContext.WalletTransactions.AddRange(clientTx, expertTx);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return new Response.DepositResultResponse
            {
                Wallet = MapToResponse(wallet),
                Transaction = MapToTxResponse(clientTx)
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new ValidationException($"Transaction failed: {ex.Message}");
        }
    }

    public async Task<Response.DepositResultResponse> ReleasePaymentFromMilestoneAsync(Guid userId, Guid milestoneId)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
                .ThenInclude(p => p.Expert)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != userId) throw new UnauthorizedException("Only client can release milestone payment.");

        // Business rule: Can only release if milestone is COMPLETED
        if (milestone.Status != MilestoneStatus.COMPLETED)
            throw new ValidationException("Can only release payment for completed milestones.");

        if (milestone.Status == MilestoneStatus.RELEASED)
            throw new ValidationException("Payment for this milestone has already been released.");

        var expertWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == milestone.Project.ExpertId);
        if (expertWallet == null) throw new NotFoundException("Expert wallet not found.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // Mark milestone as released
            milestone.Status = MilestoneStatus.RELEASED;
            milestone.ReleasedAt = DateTimeOffset.UtcNow;

            // Move from held to available balance for expert
            decimal balanceBefore = expertWallet.AvailableBalance;
            expertWallet.AvailableBalance += milestone.Amount;

            var walletTx = new WalletTransaction
            {
                WalletId = expertWallet.Id,
                UserId = milestone.Project.ExpertId,
                Amount = milestone.Amount,
                Type = WalletTransactionType.MILESTONE_RELEASE,
                Direction = TransactionDirection.CREDIT,
                PaymentId = null,
                Description = $"Milestone payment release for {milestone.Title}",
                BalanceBefore = balanceBefore,
                BalanceAfter = expertWallet.AvailableBalance
            };

            _dbContext.WalletTransactions.Add(walletTx);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return new Response.DepositResultResponse
            {
                Wallet = MapToResponse(expertWallet),
                Transaction = MapToTxResponse(walletTx)
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new ValidationException($"Transaction failed: {ex.Message}");
        }
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
