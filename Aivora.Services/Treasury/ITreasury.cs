using Aivora.Repositories.Entities;

namespace Aivora.Services.Treasury;

/// <summary>
/// Kết quả của một nghiệp vụ chuyển tiền trong Treasury.
/// Các entity được load và mutate trong cùng scoped DbContext của request —
/// caller dùng trực tiếp để map response, không query lại sau transaction.
/// PayDeposit/PayRemaining: Payments chứa đúng một Payment mới tạo.
/// Refund/Split: Payments chứa các Payment đã được cập nhật trạng thái.
/// </summary>
public sealed record TreasuryResult(
    Milestone Milestone,
    Wallet ClientWallet,
    Wallet ExpertWallet,
    IReadOnlyList<Payment> Payments);

/// <summary>
/// The Treasury (Kho bạc) - Module chịu trách nhiệm duy nhất về tính toàn vẹn tài chính,
/// luân chuyển dòng tiền và quản lý trạng thái thanh toán trong hệ thống.
/// Mọi luật chặn chuyển tiền (trạng thái Milestone, Dispute đang mở, hạn mức nợ) sống ở đây.
/// </summary>
public interface ITreasury
{
    /// <summary>
    /// Thực hiện chuyển phần tiền cọc (deposit) trực tiếp từ Client sang Expert.
    /// Tỷ lệ deposit lấy từ <see cref="Aivora.Services.Options.EscrowOptions"/>.
    /// </summary>
    Task<TreasuryResult> PayDepositAsync(Guid clientId, Guid milestoneId);

    /// <summary>
    /// Thực hiện chuyển phần tiền còn lại trực tiếp từ Client sang Expert.
    /// Từ chối khi Milestone không ở SUBMITTED hoặc còn Dispute đang mở.
    /// </summary>
    Task<TreasuryResult> PayRemainingAsync(Guid clientId, Guid milestoneId);

    /// <summary>
    /// Hoàn tiền lại cho Client.
    /// </summary>
    Task<TreasuryResult> RefundMilestoneAsync(Guid adminId, Guid milestoneId, string reason);

    /// <summary>
    /// Phân chia tiền khi có tranh chấp (Dispute).
    /// </summary>
    Task<TreasuryResult> SplitMilestoneFundsAsync(Guid milestoneId, decimal releaseToExpertAmount, decimal refundToClientAmount, string reason);

    /// <summary>
    /// Đồng bộ trạng thái Project dựa trên trạng thái các Milestone.
    /// Giải quyết bug 'hanging project' bằng cách tính cả Milestone đã REFUNDED.
    /// </summary>
    Task SyncProjectStatusAsync(Guid projectId);
}
