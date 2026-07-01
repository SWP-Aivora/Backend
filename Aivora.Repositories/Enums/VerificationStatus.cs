namespace Aivora.Repositories.Enums;

public enum VerificationStatus
{
    PENDING,           // Chờ xử lý
    VERIFIED,          // Đã được verification thành công
    REJECTED,          // Bị từ chối
    UNDER_REVIEW,      // Đang được admin review (sau khi AI fail)
    APPEAL_PENDING,    // Đang appeal
    APPEAL_APPROVED,   // Appeal được chấp nhận
    APPEAL_REJECTED    // Appeal bị từ chối
}