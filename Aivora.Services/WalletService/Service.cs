using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.NotificationService;
using Microsoft.EntityFrameworkCore;
using Aivora.Services.Extensions;

namespace Aivora.Services.WalletService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly IVNPayService _vnPayService;
    private readonly NotificationService.IService _notificationService;

    public Service(AivoraDbContext dbContext, IVNPayService vnPayService, NotificationService.IService notificationService)
    {
        _dbContext = dbContext;
        _vnPayService = vnPayService;
        _notificationService = notificationService;
    }

    public async Task<Response.WalletResponse> GetWalletAsync(Guid userId)
    {
        var wallet = await GetOrCreateWalletAsync(userId);
        return MapToResponse(wallet);
    }

    public async Task<Response.DepositResultResponse> DepositDemoAsync(Guid userId, Request.DepositDemoRequest request)
    {
        if (request.Amount <= 0) throw new ValidationException("Amount must be greater than 0.");

        var wallet = await GetOrCreateWalletAsync(userId);

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            decimal balanceBefore = wallet.AvailableBalance;
            wallet.Credit(request.Amount);

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

            // Send deposit success notification
            try
            {
                await _notificationService.SendNotificationAsync(
                    userId,
                    "Deposit successful",
                    $"You have successfully deposited {request.Amount:N0} {wallet.Currency} to your wallet.",
                    "PAYMENT",
                    "/wallet"
                );
            }
            catch
            {
                // Notification failure should not block the main business flow
            }

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

        var wallet = await GetOrCreateWalletAsync(userId);

        // In production, this would integrate with payment gateway
        // For now, we'll process it as a successful deposit
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            decimal balanceBefore = wallet.AvailableBalance;
            wallet.Credit(request.Amount);

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

            // Send deposit success notification
            try
            {
                await _notificationService.SendNotificationAsync(
                    userId,
                    "Deposit successful",
                    $"You have successfully deposited {request.Amount:N0} {wallet.Currency} to your wallet.",
                    "PAYMENT",
                    "/wallet"
                );
            }
            catch
            {
                // Notification failure should not block the main business flow
            }

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

        var wallet = await GetOrCreateWalletAsync(userId);

        var orderInfo = $"Aivora Deposit - User {userId}";
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

        var wallet = await GetOrCreateWalletAsync(userId);

        if (wallet.Debt > 0)
            throw new ValidationException("Cannot withdraw while you have an outstanding debt.");

        if (wallet.AvailableBalance < request.Amount)
            throw new ValidationException("Insufficient balance for withdrawal.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // Get fresh wallet within transaction with pessimistic lock
            Wallet currentWallet = await _dbContext.GetWalletForUpdateAsync(userId);

            if (currentWallet.Debt > 0)
                throw new ValidationException("Cannot withdraw while you have an outstanding debt.");

            if (currentWallet.AvailableBalance < request.Amount)
                throw new ValidationException("Insufficient balance for withdrawal.");

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

            // Send withdrawal success notification
            try
            {
                await _notificationService.SendNotificationAsync(
                    userId,
                    "Withdrawal successful",
                    $"Your withdrawal of {request.Amount:N0} {wallet.Currency} has been processed.",
                    "PAYMENT",
                    "/wallet"
                );
            }
            catch
            {
                // Notification failure should not block the main business flow
            }

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

        var wallet = await GetOrCreateWalletAsync(userId);

        if (wallet.AvailableBalance < request.Amount)
            throw new ValidationException("Insufficient balance for transfer.");

        var expertWallet = await GetOrCreateWalletAsync(request.RecipientId);

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
            try
            {
                currentWallet.Debit(request.Amount, bypassDebtLimit: false);
            }
            catch (InvalidOperationException ex)
            {
                throw new ValidationException(ex.Message);
            }

            // Add to expert (held until project completion)
            decimal expertBalanceBefore = currentExpertWallet.AvailableBalance;
            currentExpertWallet.Credit(request.Amount);

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
                BalanceAfter = currentWallet.AvailableBalance
            };

            var expertTx = new WalletTransaction
            {
                WalletId = expertWallet.Id,
                UserId = request.RecipientId,
                Amount = request.Amount,
                Type = WalletTransactionType.TRANSFER,
                Direction = TransactionDirection.CREDIT,
                Description = request.Description ?? $"Received from client",
                PaymentId = null,
                BalanceBefore = expertBalanceBefore,
                BalanceAfter = currentExpertWallet.AvailableBalance
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
            expertWallet.Credit(milestone.Amount);

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

    private async Task<Wallet> GetOrCreateWalletAsync(Guid userId)
    {
        var wallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet != null) return wallet;

        wallet = new Wallet
        {
            UserId = userId,
            AvailableBalance = 0,
            Currency = "AICOIN"
        };
        _dbContext.Wallets.Add(wallet);

        try
        {
            await _dbContext.SaveChangesAsync();
            return wallet;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Race condition: another concurrent request created the wallet
            // between our FirstOrDefaultAsync check and SaveChangesAsync.
            // Detach the failed attempt and re-read the now-existing wallet.
            _dbContext.Entry(wallet).State = EntityState.Detached;
            return await _dbContext.Wallets.FirstAsync(w => w.UserId == userId);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException?.Message?.Contains("23505") == true;
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
