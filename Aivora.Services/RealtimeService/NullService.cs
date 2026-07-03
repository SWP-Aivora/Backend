using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aivora.Repositories.Enums;

namespace Aivora.Services.RealtimeService;

public class NullRealtimeService : IService
{
    public Task SendJobStatusUpdateAsync(Guid userId, Guid jobId, JobStatus status, string? title) => Task.CompletedTask;
    public Task SendJobStatusUpdateToUsersAsync(IEnumerable<Guid> userIds, Guid jobId, JobStatus status, string? title) => Task.CompletedTask;
}
