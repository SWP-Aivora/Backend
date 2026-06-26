using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.MilestoneService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly ITreasury _treasury;

    public Service(AivoraDbContext dbContext, ITreasury treasury)
    {
        _dbContext = dbContext;
        _treasury = treasury;
    }

    public async Task<Response.MilestoneResponse> GetMilestoneByIdAsync(Guid userId, Guid milestoneId)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");

        if (milestone.Project.ClientId != userId && milestone.Project.ExpertId != userId)
            throw new UnauthorizedException("Access denied.");

        return MapToResponse(milestone);
    }

    public async Task<Response.MilestoneResponse> CreateMilestoneAsync(Guid userId, Guid projectId, Request.CreateMilestoneRequest request)
    {
        var project = await _dbContext.Projects.FindAsync(projectId);
        if (project == null) throw new NotFoundException("Project not found.");
        if (project.ClientId != userId) throw new UnauthorizedException("Only the client can add milestones.");
        if (project.Status == ProjectStatus.COMPLETED || project.Status == ProjectStatus.CANCELLED)
            throw new ValidationException("Cannot add milestones to a completed or cancelled project.");

        var milestone = new Milestone
        {
            ProjectId = projectId,
            Title = request.Title,
            Description = request.Description,
            AcceptanceCriteria = request.AcceptanceCriteria,
            Amount = request.Amount,
            Currency = request.Currency,
            DueDate = request.DueDate,
            OrderIndex = request.OrderIndex,
            Status = MilestoneStatus.CREATED
        };

        _dbContext.Milestones.Add(milestone);
        await _dbContext.SaveChangesAsync();

        return MapToResponse(milestone);
    }

    public async Task<Response.MilestoneResponse> UpdateMilestoneAsync(Guid userId, Guid milestoneId, Request.UpdateMilestoneRequest request)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != userId) throw new UnauthorizedException("Only the client can update milestones.");
        if (milestone.Status != MilestoneStatus.CREATED)
            throw new ValidationException("Only CREATED milestones can be updated.");

        if (request.Title != null) milestone.Title = request.Title;
        if (request.Description != null) milestone.Description = request.Description;
        if (request.AcceptanceCriteria != null) milestone.AcceptanceCriteria = request.AcceptanceCriteria;
        if (request.Amount.HasValue) milestone.Amount = request.Amount.Value;
        if (request.DueDate.HasValue) milestone.DueDate = request.DueDate.Value;
        if (request.OrderIndex.HasValue) milestone.OrderIndex = request.OrderIndex.Value;

        await _dbContext.SaveChangesAsync();
        return MapToResponse(milestone);
    }

    public async Task<Response.FundResultResponse> FundMilestoneAsync(Guid userId, Guid milestoneId)
    {
        // Sử dụng Treasury để xử lý logic phức tạp
        await _treasury.FundMilestoneAsync(userId, milestoneId);

        // Lấy lại dữ liệu sau khi Treasury xử lý xong để trả về cho UI
        var milestone = await _dbContext.Milestones.FirstAsync(m => m.Id == milestoneId);
        var clientWallet = await _dbContext.Wallets.FirstAsync(w => w.UserId == userId);
        var payment = await _dbContext.Payments.FirstAsync(p => p.MilestoneId == milestoneId && p.Status == PaymentStatus.HELD);

        return new Response.FundResultResponse
        {
            Milestone = MapToResponse(milestone),
            Payment = new Response.PaymentInfo
            {
                Id = payment.Id,
                ProjectId = payment.ProjectId,
                MilestoneId = payment.MilestoneId,
                PayerId = payment.PayerId,
                PayeeId = payment.PayeeId,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Status = payment.Status.ToString(),
                HeldAt = payment.HeldAt
            },
            Wallet = new Response.WalletInfo
            {
                AvailableBalance = clientWallet.AvailableBalance,
                HeldBalance = clientWallet.HeldBalance,
                Currency = clientWallet.Currency
            }
        };
    }

    public async Task<Response.MilestoneResponse> ApproveMilestoneAsync(Guid userId, Guid milestoneId)
    {
        // Validate milestone status at service layer before delegating to Treasury
        var milestone = await _dbContext.Milestones.FirstAsync(m => m.Id == milestoneId);
        if (milestone.Status != MilestoneStatus.SUBMITTED && milestone.Status != MilestoneStatus.DISPUTED)
            throw new ValidationException("Milestone must be in SUBMITTED or DISPUTED status to be approved.");

        // Sử dụng Treasury để giải ngân
        await _treasury.ReleaseMilestoneAsync(userId, milestoneId);

        // Refresh milestone after Treasury processing
        milestone = await _dbContext.Milestones.FirstAsync(m => m.Id == milestoneId);
        return MapToResponse(milestone);
    }

    public async Task<Response.MilestoneResponse> RequestRevisionAsync(Guid userId, Guid milestoneId, string reason)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != userId) throw new UnauthorizedException("Only the client can request revisions.");
        if (milestone.Status != MilestoneStatus.SUBMITTED)
            throw new ValidationException("Milestone must be SUBMITTED to request revision.");

        milestone.Status = MilestoneStatus.REVISION_REQUESTED;

        await _dbContext.SaveChangesAsync();
        await _treasury.SyncProjectStatusAsync(milestone.ProjectId);

        return MapToResponse(milestone);
    }

    private static Response.MilestoneResponse MapToResponse(Milestone m)
    {
        return new Response.MilestoneResponse
        {
            Id = m.Id,
            ProjectId = m.ProjectId,
            Title = m.Title,
            Description = m.Description,
            AcceptanceCriteria = m.AcceptanceCriteria,
            Amount = m.Amount,
            Currency = m.Currency,
            Status = m.Status,
            DueDate = m.DueDate,
            OrderIndex = m.OrderIndex,
            CreatedAt = m.CreatedAt,
            FundedAt = m.FundedAt
        };
    }
}
