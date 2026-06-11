using Aivora.Repositories.Abstractions;

namespace Aivora.Repositories.Entities;

/// <summary>
/// JobPostMilestone - Đại diện cho các cột mốc dự kiến trong một tin tuyển dụng.
/// Được sử dụng để lưu lại các gợi ý từ AI hoặc kế hoạch của Client trước khi Expert nộp Proposal.
/// </summary>
public class JobPostMilestone : AuditableBaseEntity
{
    public Guid JobPostId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public int DueDays { get; set; }
    public string? AcceptanceCriteria { get; set; }
    public int OrderIndex { get; set; }

    // Navigation Properties
    public virtual JobPost JobPost { get; set; } = null!;
}
