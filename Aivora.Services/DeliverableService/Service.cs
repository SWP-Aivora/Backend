using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.NotificationService;
using Aivora.Services.Treasury;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.DeliverableService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly ITreasury _treasury;
    private readonly NotificationService.IService _notificationService;

    public Service(AivoraDbContext dbContext, ITreasury treasury, NotificationService.IService notificationService)
    {
        _dbContext = dbContext;
        _treasury = treasury;
        _notificationService = notificationService;
    }

    public async Task<Response.DeliverableResponse> SubmitDeliverableAsync(Guid expertId, Guid milestoneId, Request.SubmitDeliverableRequest request)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ExpertId != expertId) throw new ForbiddenException("Only the project expert can submit deliverables.");
        if (milestone.Status == MilestoneStatus.DISPUTED || milestone.Project.Status == ProjectStatus.DISPUTED)
            throw new ValidationException("Cannot submit a deliverable while there is an active dispute.");

        if (milestone.Status != MilestoneStatus.FUNDED &&
            milestone.Status != MilestoneStatus.IN_PROGRESS &&
            milestone.Status != MilestoneStatus.REVISION_REQUESTED)
            throw new ValidationException("Cannot submit deliverable for this milestone status.");

        if (string.IsNullOrWhiteSpace(request.FileUrl) &&
            string.IsNullOrWhiteSpace(request.DemoUrl) &&
            string.IsNullOrWhiteSpace(request.SourceCodeUrl) &&
            string.IsNullOrWhiteSpace(request.Note))
        {
            throw new ValidationException("At least one evidence field (FileUrl, DemoUrl, SourceCodeUrl, Note) must be provided.");
        }

        var latestRevision = await _dbContext.Deliverables
            .Where(d => d.MilestoneId == milestoneId)
            .OrderByDescending(d => d.RevisionNumber)
            .Select(d => d.RevisionNumber)
            .FirstOrDefaultAsync();

        var deliverable = new Deliverable
        {
            MilestoneId = milestoneId,
            ExpertId = expertId,
            Description = request.Description,
            FileUrl = request.FileUrl,
            DemoUrl = request.DemoUrl,
            SourceCodeUrl = request.SourceCodeUrl,
            Note = request.Note,
            RevisionNumber = latestRevision + 1,
            Status = DeliverableStatus.SUBMITTED
        };

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            _dbContext.Deliverables.Add(deliverable);

            milestone.Status = MilestoneStatus.SUBMITTED;
            milestone.SubmittedAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync();
            await _treasury.SyncProjectStatusAsync(milestone.ProjectId);

            await transaction.CommitAsync();

            // Send notification to the Client that the Expert has submitted a deliverable
            try
            {
                await _notificationService.SendNotificationAsync(
                    milestone.Project.ClientId,
                    "Expert has submitted a deliverable",
                    $"The expert has submitted a deliverable for milestone \"{milestone.Title}\". Please review and evaluate.",
                    "MILESTONE",
                    $"/projects/{milestone.ProjectId}/milestones/{milestoneId}"
                );
            }
            catch
            {
                // Notification failure should not block the main business flow
            }

            return MapToResponse(deliverable);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<Response.DeliverableResponse>> GetDeliverablesByMilestoneAsync(Guid userId, Guid milestoneId)
    {
        var milestone = await _dbContext.Milestones
            .Include(m => m.Project)
            .FirstOrDefaultAsync(m => m.Id == milestoneId);

        if (milestone == null) throw new NotFoundException("Milestone not found.");
        if (milestone.Project.ClientId != userId && milestone.Project.ExpertId != userId)
            throw new ForbiddenException("Access denied.");

        var deliverables = await _dbContext.Deliverables
            .Where(d => d.MilestoneId == milestoneId)
            .OrderByDescending(d => d.RevisionNumber)
            .ToListAsync();

        return deliverables.Select(MapToResponse).ToList();
    }

    private static Response.DeliverableResponse MapToResponse(Deliverable d)
    {
        return new Response.DeliverableResponse
        {
            Id = d.Id,
            MilestoneId = d.MilestoneId,
            ExpertId = d.ExpertId,
            Description = d.Description ?? "",
            FileUrl = d.FileUrl,
            DemoUrl = d.DemoUrl,
            SourceCodeUrl = d.SourceCodeUrl,
            Note = d.Note,
            RevisionNumber = d.RevisionNumber,
            Status = d.Status,
            SubmittedAt = d.CreatedAt,
            ReviewedAt = d.ReviewedAt
        };
    }
}
