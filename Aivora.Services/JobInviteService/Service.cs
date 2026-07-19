using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.JobInviteService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly NotificationService.IService _notificationService;

    public Service(AivoraDbContext dbContext, NotificationService.IService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    public async Task<Response.JobInviteResponse> CreateInviteAsync(Guid clientId, Guid jobId, Request.CreateJobInviteRequest request)
    {
        if (request is null) throw new ValidationException("Request body is required.");

        var job = await _dbContext.JobPosts.FirstOrDefaultAsync(j => j.Id == jobId);
        if (job == null) throw new NotFoundException("Job not found.");
        if (job.ClientId != clientId) throw new ForbiddenException("Only the job owner can invite experts.");
        if (job.Status != JobStatus.OPEN) throw new ValidationException("Job is no longer open for invites.");

        var expert = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.ExpertId);
        if (expert == null || expert.Role != UserRole.EXPERT) throw new NotFoundException("Expert not found.");

        var alreadyInvited = await _dbContext.JobInvites.AnyAsync(i => i.JobId == jobId && i.ExpertId == request.ExpertId);
        if (alreadyInvited) throw new ValidationException("This expert has already been invited to this job.");

        var invite = new JobInvite
        {
            JobId = jobId,
            ExpertId = request.ExpertId,
            ClientId = clientId,
            Status = JobInviteStatus.PENDING
        };

        _dbContext.JobInvites.Add(invite);
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Race condition: another concurrent invite for this Job+Expert committed between
            // our AnyAsync check and SaveChangesAsync. The partial unique index caught it.
            throw new ValidationException("This expert has already been invited to this job.");
        }

        try
        {
            await _notificationService.SendNotificationAsync(
                expert.Id,
                "New job invite",
                $"You have been invited to apply for the job \"{job.Title}\".",
                "JOB_INVITE",
                $"/jobs/{job.Id}"
            );
        }
        catch
        {
            // Notification failure should not block the main business flow
        }

        return await GetInviteByIdAsync(invite.Id);
    }

    public async Task<Response.JobInviteResponse> AcceptInviteAsync(Guid expertId, Guid inviteId)
    {
        var invite = await LoadOwnedPendingInviteAsync(expertId, inviteId, "accepted");

        invite.Status = JobInviteStatus.ACCEPTED;
        invite.RespondedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync();

        return await GetInviteByIdAsync(invite.Id);
    }

    public async Task<Response.JobInviteResponse> DeclineInviteAsync(Guid expertId, Guid inviteId)
    {
        var invite = await LoadOwnedPendingInviteAsync(expertId, inviteId, "declined");

        invite.Status = JobInviteStatus.DECLINED;
        invite.RespondedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync();

        return await GetInviteByIdAsync(invite.Id);
    }

    public async Task<List<Response.JobInviteResponse>> GetInvitesByJobAsync(Guid clientId, Guid jobId)
    {
        var job = await _dbContext.JobPosts.FirstOrDefaultAsync(j => j.Id == jobId);
        if (job == null) throw new NotFoundException("Job not found.");
        if (job.ClientId != clientId) throw new ForbiddenException("Only the job owner can view its invites.");

        var invites = await _dbContext.JobInvites
            .Include(i => i.Job)
            .Include(i => i.Expert)
            .Where(i => i.JobId == jobId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return invites.Select(MapToResponse).ToList();
    }

    public async Task<List<Response.JobInviteResponse>> GetMyInvitesForExpertAsync(Guid expertId, JobInviteStatus? status)
    {
        var query = _dbContext.JobInvites
            .Include(i => i.Job)
            .Include(i => i.Expert)
            .Where(i => i.ExpertId == expertId);

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        var invites = await query.OrderByDescending(i => i.CreatedAt).ToListAsync();

        return invites.Select(MapToResponse).ToList();
    }

    private async Task<JobInvite> LoadOwnedPendingInviteAsync(Guid expertId, Guid inviteId, string action)
    {
        var invite = await _dbContext.JobInvites.FirstOrDefaultAsync(i => i.Id == inviteId);

        if (invite == null) throw new NotFoundException("Job invite not found.");
        if (invite.ExpertId != expertId) throw new ForbiddenException("Only the invited expert can respond to this invite.");
        if (invite.Status != JobInviteStatus.PENDING) throw new ValidationException($"Only pending invites can be {action}.");

        return invite;
    }

    private async Task<Response.JobInviteResponse> GetInviteByIdAsync(Guid id)
    {
        var invite = await _dbContext.JobInvites
            .Include(i => i.Job)
            .Include(i => i.Expert)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invite == null) throw new NotFoundException("Job invite not found.");

        return MapToResponse(invite);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException?.Message?.Contains("23505") == true;
    }

    private static Response.JobInviteResponse MapToResponse(JobInvite invite)
    {
        return new Response.JobInviteResponse
        {
            Id = invite.Id,
            JobId = invite.JobId,
            JobTitle = invite.Job?.Title ?? "N/A",
            ExpertId = invite.ExpertId,
            ExpertName = invite.Expert?.FullName ?? "N/A",
            ClientId = invite.ClientId,
            Status = invite.Status,
            RespondedAt = invite.RespondedAt,
            CreatedAt = invite.CreatedAt
        };
    }
}
