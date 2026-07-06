namespace Aivora.Services.Treasury;

/// <summary>
/// The Treasury (Kho bạc) - Module chịu trách nhiệm duy nhất về tính toàn vẹn tài chính,
/// luân chuyển dòng tiền và quản lý trạng thái thanh toán trong hệ thống.
/// </summary>
public interface ITreasury
{
    /// <summary>
    /// Thực hiện chuyển 30% tiền cọc trực tiếp từ Client sang Expert.
    /// </summary>
    Task PayDepositAsync(Guid clientId, Guid milestoneId);

    /// <summary>
    /// Thực hiện chuyển 70% tiền còn lại trực tiếp từ Client sang Expert.
    /// </summary>
    Task PayRemainingAsync(Guid clientId, Guid milestoneId);

    /// <summary>
    /// Hoàn tiền lại cho Client.
    /// </summary>
    Task RefundMilestoneAsync(Guid adminId, Guid milestoneId, string reason);

    /// <summary>
    /// Phân chia tiền khi có tranh chấp (Dispute).
    /// </summary>
    Task SplitMilestoneFundsAsync(Guid milestoneId, decimal releaseToExpertAmount, decimal refundToClientAmount, string reason);

    /// <summary>
    /// Đồng bộ trạng thái Project dựa trên trạng thái các Milestone.
    /// Giải quyết bug 'hanging project' bằng cách tính cả Milestone đã REFUNDED.
    /// </summary>
    Task SyncProjectStatusAsync(Guid projectId);
}
