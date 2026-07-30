using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aivora.Repositories.Enums;

namespace Aivora.Services.RealtimeService;

public interface IService
{
    Task SendJobStatusUpdateAsync(Guid userId, Guid jobId, JobStatus status, string? title);
    Task SendJobStatusUpdateToUsersAsync(IEnumerable<Guid> userIds, Guid jobId, JobStatus status, string? title);
    Task SendNewJobPublishedAsync(Guid jobId, string title);

    // ponytail: fire-and-forget (void, not Task) - see Service.cs for why. Callers just call it, no await.
    void SendMilestoneUpdatedAsync(Guid projectId, Guid milestoneId);
    void SendDisputeUpdatedAsync(Guid projectId, Guid disputeId);
}
