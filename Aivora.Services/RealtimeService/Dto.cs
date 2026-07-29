using System;

namespace Aivora.Services.RealtimeService;

public class RealtimeJobStatusUpdateDto
{
    public Guid JobId { get; set; }
    public string Status { get; set; } = null!;
    public string? Title { get; set; }
}

public class MilestoneUpdatedDto
{
    public Guid ProjectId { get; set; }
    public Guid MilestoneId { get; set; }
}
