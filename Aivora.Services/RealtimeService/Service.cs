using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aivora.api.Hubs;
using Aivora.Repositories.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Aivora.Services.RealtimeService;

public class Service : IService
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<Service>? _logger;

    public Service(IHubContext<ChatHub> hubContext, ILogger<Service>? logger = null)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendJobStatusUpdateAsync(Guid userId, Guid jobId, JobStatus status, string? title)
    {
        var statusStr = status.ToString().ToLowerInvariant().Replace('_', '-');
        var payload = new RealtimeJobStatusUpdateDto
        {
            JobId = jobId,
            Status = statusStr,
            Title = title
        };
        await _hubContext.Clients.User(userId.ToString()).SendAsync("JobStatusUpdated", payload);
    }

    public Task SendJobStatusUpdateToUsersAsync(IEnumerable<Guid> userIds, Guid jobId, JobStatus status, string? title)
    {
        var tasks = new List<Task>();
        foreach (var userId in userIds)
        {
            tasks.Add(SendJobStatusUpdateAsync(userId, jobId, status, title));
        }
        return Task.WhenAll(tasks);
    }

    public async Task SendNewJobPublishedAsync(Guid jobId, string title)
    {
        await _hubContext.Clients.All.SendAsync("NewJobPublished", new { JobId = jobId, Title = title });
    }

    // Fire-and-forget (void, not Task): some callers (e.g. Treasury.PayDepositAsync) commit a DB
    // transaction right before this call inside a try/catch that calls RollbackAsync() on failure.
    // If this were awaited and threw, that catch would try to roll back an already-committed
    // transaction. void + Task.Run + internal try/catch means it can never throw into the caller.
    public void SendMilestoneUpdatedAsync(Guid projectId, Guid milestoneId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var payload = new MilestoneUpdatedDto { ProjectId = projectId, MilestoneId = milestoneId };
                await _hubContext.Clients.Group($"project-{projectId}").SendAsync("MilestoneUpdated", payload);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to broadcast MilestoneUpdated for project {ProjectId}, milestone {MilestoneId}", projectId, milestoneId);
            }
        });
    }

    // Same fire-and-forget shape as SendMilestoneUpdatedAsync — see the comment above for why.
    public void SendDisputeUpdatedAsync(Guid projectId, Guid disputeId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var payload = new DisputeUpdatedDto { ProjectId = projectId, DisputeId = disputeId };
                await _hubContext.Clients.Group($"project-{projectId}").SendAsync("DisputeUpdated", payload);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to broadcast DisputeUpdated for project {ProjectId}, dispute {DisputeId}", projectId, disputeId);
            }
        });
    }
}
