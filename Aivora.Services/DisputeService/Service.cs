using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.DisputeService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;

    public Service(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Response.DisputeResponse> OpenDisputeAsync(Guid userId, Request.OpenDisputeRequest request)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == request.MilestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != userId && milestone.Project.ExpertId != userId)
            throw new UnauthorizedException("You are not authorized to open a dispute for this project.");

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestone.Id && p.Status == PaymentStatus.HELD);
        if (payment == null) throw new ValidationException("Only funded milestones with held payments can be disputed.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var againstUserId = (userId == milestone.Project.ClientId) ? milestone.Project.ExpertId : milestone.Project.ClientId;

            var dispute = new Dispute
            {
                ProjectId = milestone.ProjectId,
                MilestoneId = milestone.Id,
                PaymentId = payment.Id,
                OpenedBy = userId,
                AgainstUserId = againstUserId,
                Reason = request.Reason,
                Description = request.Description,
                Status = DisputeStatus.OPEN
            };

            milestone.Status = MilestoneStatus.DISPUTED;
            milestone.Project.Status = ProjectStatus.DISPUTED;
            payment.Status = PaymentStatus.FROZEN;

            _dbContext.Disputes.Add(dispute);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return await GetDisputeByIdAsync(userId, dispute.Id);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Response.DisputeResponse> GetDisputeByIdAsync(Guid userId, Guid disputeId)
    {
        var dispute = await _dbContext.Disputes
            .Include(d => d.Project)
            .Include(d => d.Milestone)
            .Include(d => d.Opener)
            .Include(d => d.Admin)
            .Include(d => d.Milestone.Project.Expert) // To get against user name
            .Include(d => d.Milestone.Project.Client)
            .FirstOrDefaultAsync(d => d.Id == disputeId);

        if (dispute == null) throw new NotFoundException("Dispute not found.");
        
        // Basic auth check (participating users or admin)
        var user = await _dbContext.Users.FindAsync(userId);
        if (user!.Role != UserRole.ADMIN && dispute.OpenedBy != userId && dispute.AgainstUserId != userId)
            throw new UnauthorizedException("You are not authorized to view this dispute.");

        var againstUser = await _dbContext.Users.FindAsync(dispute.AgainstUserId);
        var evidence = await _dbContext.DisputeEvidences
            .Include(e => e.SubmittedByUser)
            .Where(e => e.DisputeId == disputeId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        return new Response.DisputeResponse
        {
            Id = dispute.Id,
            ProjectId = dispute.ProjectId,
            ProjectTitle = dispute.Project.Title,
            MilestoneId = dispute.MilestoneId,
            MilestoneTitle = dispute.Milestone.Title,
            OpenedBy = dispute.OpenedBy,
            OpenerName = dispute.Opener.FullName,
            AgainstUserId = dispute.AgainstUserId,
            AgainstUserName = againstUser!.FullName,
            Reason = dispute.Reason,
            Description = dispute.Description,
            Status = dispute.Status.ToString(),
            ResolutionType = dispute.ResolutionType?.ToString(),
            ResolutionNote = dispute.ResolutionNote,
            ResolvedAt = dispute.ResolvedAt,
            CreatedAt = dispute.CreatedAt,
            Evidence = evidence.Select(e => new Response.DisputeEvidenceResponse
            {
                Id = e.Id,
                SubmittedBy = e.SubmittedBy,
                SubmittedByName = e.SubmittedByUser.FullName,
                Content = e.Content ?? "",
                FileUrl = e.FileUrl,
                CreatedAt = e.CreatedAt
            }).ToList()
        };
    }

    public async Task<Base.Response.PageResult<Response.DisputeResponse>> GetDisputesAsync(Guid userId, string role, Base.Request.PageRequest pageRequest)
    {
        IQueryable<Dispute> query = _dbContext.Disputes
            .Include(d => d.Project)
            .Include(d => d.Milestone)
            .Include(d => d.Opener);

        if (role != UserRole.ADMIN.ToString())
        {
            query = query.Where(d => d.OpenedBy == userId || d.AgainstUserId == userId);
        }

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((pageRequest.PageIndex - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .ToListAsync();

        var responses = new List<Response.DisputeResponse>();
        foreach (var d in items)
        {
            // Simplified mapping for list
            responses.Add(new Response.DisputeResponse
            {
                Id = d.Id,
                ProjectId = d.ProjectId,
                ProjectTitle = d.Project.Title,
                MilestoneId = d.MilestoneId,
                MilestoneTitle = d.Milestone.Title,
                OpenedBy = d.OpenedBy,
                OpenerName = d.Opener.FullName,
                Reason = d.Reason,
                Status = d.Status.ToString(),
                CreatedAt = d.CreatedAt
            });
        }

        return new Base.Response.PageResult<Response.DisputeResponse>
        {
            Items = responses,
            TotalItems = totalItems,
            PageIndex = pageRequest.PageIndex,
            PageSize = pageRequest.PageSize
        };
    }

    public async Task<Response.DisputeEvidenceResponse> AddEvidenceAsync(Guid userId, Guid disputeId, Request.AddEvidenceRequest request)
    {
        var dispute = await _dbContext.Disputes.FindAsync(disputeId);
        if (dispute == null) throw new NotFoundException("Dispute not found.");
        if (dispute.Status == DisputeStatus.RESOLVED || dispute.Status == DisputeStatus.CLOSED)
            throw new ValidationException("Cannot add evidence to a closed dispute.");

        var user = await _dbContext.Users.FindAsync(userId);
        if (user!.Role != UserRole.ADMIN && dispute.OpenedBy != userId && dispute.AgainstUserId != userId)
            throw new UnauthorizedException("You are not authorized to add evidence to this dispute.");

        var evidence = new DisputeEvidence
        {
            DisputeId = disputeId,
            SubmittedBy = userId,
            Content = request.Content,
            FileUrl = request.FileUrl
        };

        _dbContext.DisputeEvidences.Add(evidence);
        await _dbContext.SaveChangesAsync();

        return new Response.DisputeEvidenceResponse
        {
            Id = evidence.Id,
            SubmittedBy = userId,
            SubmittedByName = user.FullName,
            Content = evidence.Content ?? "",
            FileUrl = evidence.FileUrl,
            CreatedAt = evidence.CreatedAt
        };
    }

    public async Task<Response.DisputeResponse> ResolveDisputeAsync(Guid adminId, Guid disputeId, Request.ResolveDisputeRequest request)
    {
        var dispute = await _dbContext.Disputes
            .Include(d => d.Milestone)
            .Include(d => d.Project)
            .Include(d => d.Payment)
            .FirstOrDefaultAsync(d => d.Id == disputeId);

        if (dispute == null) throw new NotFoundException("Dispute not found.");
        if (dispute.Status == DisputeStatus.RESOLVED) throw new ValidationException("Dispute is already resolved.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            dispute.Status = DisputeStatus.RESOLVED;
            dispute.ResolutionType = request.ResolutionType;
            dispute.ResolutionNote = request.ResolutionNote;
            dispute.ResolvedAt = DateTimeOffset.UtcNow;
            dispute.AdminId = adminId;

            var payment = dispute.Payment;
            var project = dispute.Project;
            var milestone = dispute.Milestone;

            switch (request.ResolutionType)
            {
                case DisputeResolutionType.RELEASE_TO_EXPERT:
                    payment.Status = PaymentStatus.RELEASED;
                    milestone.Status = MilestoneStatus.PAID;
                    // Logic to update expert wallet (simplified here, should call WalletService)
                    var expertWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == project.ExpertId);
                    var clientWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == project.ClientId);
                    
                    clientWallet!.HeldBalance -= payment.Amount;
                    expertWallet!.AvailableBalance += payment.Amount;
                    expertWallet!.TotalEarned += payment.Amount;
                    break;

                case DisputeResolutionType.REFUND_TO_CLIENT:
                    payment.Status = PaymentStatus.REFUNDED;
                    milestone.Status = MilestoneStatus.REFUNDED;
                    var cWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == project.ClientId);
                    cWallet!.HeldBalance -= payment.Amount;
                    cWallet!.AvailableBalance += payment.Amount;
                    break;

                case DisputeResolutionType.SPLIT_PAYMENT:
                    if (request.ReleaseAmount + request.RefundAmount != payment.Amount)
                        throw new ValidationException("Total split amounts must equal payment amount.");
                    
                    payment.Status = PaymentStatus.RELEASED; // Or a custom status like PARTIALLY_RELEASED
                    milestone.Status = MilestoneStatus.PAID;

                    var eWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == project.ExpertId);
                    var clWallet = await _dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == project.ClientId);

                    clWallet!.HeldBalance -= payment.Amount;
                    clWallet!.AvailableBalance += request.RefundAmount ?? 0;
                    eWallet!.AvailableBalance += request.ReleaseAmount ?? 0;
                    eWallet!.TotalEarned += request.ReleaseAmount ?? 0;
                    break;
            }

            // Check if all milestones are paid/refunded to complete project
            var allFinished = await _dbContext.Milestones
                .Where(m => m.ProjectId == project.Id && m.Id != milestone.Id)
                .AllAsync(m => m.Status == MilestoneStatus.PAID || m.Status == MilestoneStatus.REFUNDED);
            
            if (allFinished) project.Status = ProjectStatus.COMPLETED;
            else project.Status = ProjectStatus.ACTIVE;

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return await GetDisputeByIdAsync(adminId, disputeId);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
