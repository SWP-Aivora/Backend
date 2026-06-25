namespace Aivora.Services.Treasury;

/// <summary>
/// The Treasury (Kho bạc) - Module chịu trách nhiệm duy nhất về tính toàn vẹn tài chính mô phỏng,
/// luân chuyển dòng tiền demo và quản lý trạng thái thanh toán trực tiếp trong hệ thống.
/// </summary>
public interface ITreasury
{
    /// <summary>
    /// Thực hiện ghi nhận dòng tiền chuyển trực tiếp mô phỏng cho một Milestone.
    /// Bao gồm: Kiểm tra số dư demo, cập nhật số dư demo khả dụng và số dư treo kỹ thuật (held), tạo giao dịch mô phỏng, cập nhật Milestone và Project.
    /// </summary>
    Task FundMilestoneAsync(Guid clientId, Guid milestoneId);

    /// <summary>
    /// Ghi nhận hoàn thành giao dịch trực tiếp mô phỏng cho Expert sau khi Milestone hoàn thành.
    /// </summary>
    Task ReleaseMilestoneAsync(Guid clientId, Guid milestoneId);

    /// <summary>
    /// Ghi nhận hoàn tiền trực tiếp mô phỏng lại cho Client.
    /// </summary>
    Task RefundMilestoneAsync(Guid adminId, Guid milestoneId, decimal amount, string reason);

    /// <summary>
    /// Phân chia khoản tiền ghi nhận trực tiếp mô phỏng khi có tranh chấp (Dispute).
    /// </summary>
    Task SplitMilestoneFundsAsync(Guid milestoneId, decimal releaseToExpertAmount, decimal refundToClientAmount, string reason);

    /// <summary>
    /// Ghi nhận trạng thái treo thanh toán (ví dụ: khi có tranh chấp).
    /// </summary>
    Task FreezeFundsAsync(Guid milestoneId, string reason);

    /// <summary>
    /// Giải phóng trạng thái treo thanh toán ghi nhận.
    /// </summary>
    Task UnfreezeFundsAsync(Guid milestoneId, string reason);

    /// <summary>
    /// Đồng bộ trạng thái Project dựa trên trạng thái các Milestone.
    /// Giải quyết bug 'hanging project' bằng cách tính cả Milestone đã REFUNDED.
    /// </summary>
    Task SyncProjectStatusAsync(Guid projectId);

    /// <summary>
    /// Chuyển Project sang trạng thái DISPUTED.
    /// </summary>
    Task MarkProjectDisputedAsync(Guid projectId);
}
