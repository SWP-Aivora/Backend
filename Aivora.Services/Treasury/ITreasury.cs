namespace Aivora.Services.Treasury;

/// <summary>
/// The Treasury (Kho bạc) - Module chịu trách nhiệm duy nhất về tính toàn vẹn tài chính,
/// luân chuyển dòng tiền và quản lý trạng thái thanh toán trong hệ thống.
/// </summary>
public interface ITreasury
{
    /// <summary>
    /// Thực hiện nộp tiền ký quỹ (Escrow) cho một Milestone.
    /// Bao gồm: Kiểm tra ví, trừ tiền ví khả dụng, tăng tiền treo (held), tạo giao dịch, cập nhật Milestone và Project.
    /// </summary>
    Task FundMilestoneAsync(Guid clientId, Guid milestoneId);

    /// <summary>
    /// Giải ngân tiền từ Escrow cho Expert sau khi Milestone hoàn thành.
    /// </summary>
    Task ReleaseMilestoneAsync(Guid clientId, Guid milestoneId);

    /// <summary>
    /// Hoàn tiền từ Escrow lại cho Client.
    /// </summary>
    Task RefundMilestoneAsync(Guid adminId, Guid milestoneId, decimal amount, string reason);

    /// <summary>
    /// Phân chia tiền ký quỹ khi có tranh chấp (Dispute).
    /// </summary>
    Task SplitMilestoneFundsAsync(Guid milestoneId, decimal releaseToExpertAmount, decimal refundToClientAmount, string reason);

    /// <summary>
    /// Đóng băng tiền ký quỹ (ví dụ: khi có tranh chấp).
    /// </summary>
    Task FreezeFundsAsync(Guid milestoneId, string reason);

    /// <summary>
    /// Giải phóng trạng thái đóng băng cho tiền ký quỹ.
    /// </summary>
    Task UnfreezeFundsAsync(Guid milestoneId, string reason);
}
