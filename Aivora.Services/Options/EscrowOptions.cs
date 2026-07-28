using System.ComponentModel.DataAnnotations;

namespace Aivora.Services.Options;

/// <summary>
/// Tỷ lệ chia đôi thanh toán milestone (deposit trả ngay khi bắt đầu / phần còn lại trả khi duyệt).
/// Cảnh báo: đổi giá trị này khi đã có Payment cũ trong DB có thể làm Treasury phân loại nhầm
/// payment lịch sử (xem comment tại Treasury.RefundMilestoneAsync).
/// </summary>
public class EscrowOptions
{
    [Range(0.01, 0.99, ErrorMessage = "Deposit rate must be between 0.01 and 0.99")]
    public decimal DepositRate { get; set; } = 0.30m;

    public decimal RemainingRate => 1m - DepositRate;
}
