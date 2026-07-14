using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aivora.api.Hubs;
using Aivora.Repositories.Enums;
using Microsoft.AspNetCore.SignalR;

namespace Aivora.Services.RealtimeService;

public class Service : IService
{
    private readonly IHubContext<ChatHub> _hubContext;

    public Service(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
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
}
