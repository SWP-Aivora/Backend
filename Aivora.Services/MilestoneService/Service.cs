using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Repositories.Repositories.Milestones;
using Aivora.Repositories.Repositories.Treasury;
using Aivora.Services.Exceptions;
using Aivora.Services.Treasury;

namespace Aivora.Services.MilestoneService;

public class MilestoneApplicationService : IService
{
    private readonly IMilestoneRepository _milestoneRepository;
    private readonly ITreasuryRepository _treasuryRepository;
    private readonly ITreasury _treasury;

    public MilestoneApplicationService(
        IMilestoneRepository milestoneRepository,
        ITreasuryRepository treasuryRepository,
        ITreasury treasury)
    {
        _milestoneRepository = milestoneRepository;
        _treasuryRepository = treasuryRepository;
        _treasury = treasury;
    }

    public async Task<Response.MilestoneResponse> GetMilestoneByIdAsync(Guid userId, Guid milestoneId)
    {
        var milestone = await _milestoneRepository.GetWithProjectAsync(milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");

        if (milestone.Project.ClientId != userId && milestone.Project.ExpertId != userId)
            throw new UnauthorizedException("Access denied.");

        return MapToResponse(milestone);
    }

    public async Task<Response.MilestoneResponse> CreateMilestoneAsync(Guid userId, Guid projectId, Request.CreateMilestoneRequest request)
    {
        var project = await _milestoneRepository.GetProjectByIdAsync(projectId);
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

        await _milestoneRepository.AddAsync(milestone);
        await _milestoneRepository.SaveChangesAsync();

        return MapToResponse(milestone);
    }

    public async Task<Response.MilestoneResponse> UpdateMilestoneAsync(Guid userId, Guid milestoneId, Request.UpdateMilestoneRequest request)
    {
        var milestone = await _milestoneRepository.GetWithProjectAsync(milestoneId);

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

        await _milestoneRepository.SaveChangesAsync();
        return MapToResponse(milestone);
    }

    public async Task<Response.FundResultResponse> FundMilestoneAsync(Guid userId, Guid milestoneId)
    {
        // Sử dụng Treasury để xử lý logic phức tạp
        await _treasury.FundMilestoneAsync(userId, milestoneId);

        // Lấy lại dữ liệu sau khi Treasury xử lý xong để trả về cho UI
        var milestone = await _milestoneRepository.GetByIdAsync(milestoneId)
            ?? throw new NotFoundException("Milestone not found.");
        var clientWallet = await _treasuryRepository.GetWalletByUserIdAsync(userId)
            ?? throw new NotFoundException($"Wallet for user {userId} not found.");
        var payment = await _treasuryRepository.GetPaymentByMilestoneAndStatusAsync(milestoneId, PaymentStatus.HELD)
            ?? throw new NotFoundException("Held payment not found for this milestone.");

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
        // Sử dụng Treasury để giải ngân
        await _treasury.ReleaseMilestoneAsync(userId, milestoneId);

        var milestone = await _milestoneRepository.GetByIdAsync(milestoneId)
            ?? throw new NotFoundException("Milestone not found.");
        return MapToResponse(milestone);
    }

    public async Task<Response.MilestoneResponse> RequestRevisionAsync(Guid userId, Guid milestoneId, string reason)
    {
        var milestone = await _milestoneRepository.GetWithProjectAsync(milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != userId) throw new UnauthorizedException("Only the client can request revisions.");
        if (milestone.Status != MilestoneStatus.SUBMITTED)
            throw new ValidationException("Milestone must be SUBMITTED to request revision.");

        milestone.Status = MilestoneStatus.REVISION_REQUESTED;

        await _milestoneRepository.SaveChangesAsync();
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
