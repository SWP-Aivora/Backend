using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.FinancialLedger;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aivora.Services.Treasury;

public class Treasury : ITreasury
{
    private readonly AivoraDbContext _dbContext;
    private readonly IFinancialLedger _ledger;
    private readonly ILogger<Treasury> _logger;

    public Treasury(AivoraDbContext dbContext, IFinancialLedger ledger, ILogger<Treasury> logger)
    {
        _dbContext = dbContext;
        _ledger = ledger;
        _logger = logger;
    }

    public async Task FundMilestoneAsync(Guid clientId, Guid milestoneId)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);
        
        if (milestone.Project.ClientId != clientId) throw new UnauthorizedException("Access denied.");
        if (milestone.Status != MilestoneStatus.CREATED) throw new ValidationException("Milestone is already funded or processed.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            await _ledger.EscrowFundsAsync(clientId, milestoneId, milestone.Amount, $"Funding milestone: {milestone.Title}");

            milestone.Status = MilestoneStatus.FUNDED;
            milestone.FundedAt = DateTimeOffset.UtcNow;

            if (milestone.Project.Status == ProjectStatus.PENDING_PAYMENT)
            {
                milestone.Project.Status = ProjectStatus.ACTIVE;
                milestone.Project.StartDate = DateOnly.FromDateTime(DateTime.UtcNow);
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            
            _logger?.LogInformation("✅ Milestone {MilestoneId} funded successfully by Client {ClientId}", milestoneId, clientId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger?.LogError(ex, "❌ Failed to fund milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task ReleaseMilestoneAsync(Guid clientId, Guid milestoneId)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);

        if (milestone.Project.ClientId != clientId) throw new UnauthorizedException("Only the client can approve and release funds.");
        if (milestone.Status != MilestoneStatus.SUBMITTED) throw new ValidationException("Milestone must be in SUBMITTED status to be released.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            await _ledger.ReleaseFundsAsync(milestoneId, milestone.Amount, $"Payment released for milestone: {milestone.Title}");

            milestone.Status = MilestoneStatus.PAID;
            milestone.ApprovedAt = DateTimeOffset.UtcNow;
            milestone.PaidAt = DateTimeOffset.UtcNow;

            if (milestone.Project.Milestones.All(m => m.Status == MilestoneStatus.PAID))
            {
                milestone.Project.Status = ProjectStatus.COMPLETED;
                milestone.Project.CompletedAt = DateTimeOffset.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger?.LogInformation("✅ Funds released for Milestone {MilestoneId}", milestoneId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger?.LogError(ex, "❌ Failed to release funds for Milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task RefundMilestoneAsync(Guid clientId, Guid milestoneId, decimal amount, string reason)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            await _ledger.RefundFundsAsync(milestoneId, amount, $"Refund for milestone: {reason}");

            milestone.Status = MilestoneStatus.REFUNDED;
            
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger?.LogError(ex, "❌ Failed to refund milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    public async Task SplitMilestoneFundsAsync(Guid milestoneId, decimal releaseToExpertAmount, decimal refundToClientAmount, string reason)
    {
        var milestone = await GetMilestoneWithProjectAsync(milestoneId);

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            await _ledger.SplitFundsAsync(milestoneId, releaseToExpertAmount, refundToClientAmount, $"Dispute resolved: Split payment. {reason}");

            // If any amount was paid to expert, we mark as PAID (or could have a PARTIAL status, but PAID is simpler for lifecycle)
            milestone.Status = releaseToExpertAmount > 0 ? MilestoneStatus.PAID : MilestoneStatus.REFUNDED;

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger?.LogError(ex, "❌ Failed to split funds for milestone {MilestoneId}", milestoneId);
            throw;
        }
    }

    private async Task<Milestone> GetMilestoneWithProjectAsync(Guid milestoneId)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project).ThenInclude(p => p.Milestones)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        return milestone ?? throw new NotFoundException("Milestone not found.");
    }
}
