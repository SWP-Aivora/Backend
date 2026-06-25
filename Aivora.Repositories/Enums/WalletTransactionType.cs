using System.Text.Json.Serialization;

namespace Aivora.Repositories.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WalletTransactionType
{
    DEMO_DEPOSIT,
    DEPOSIT,
    ESCROW_HOLD,
    PAYMENT_RELEASE,
    REFUND,
    WITHDRAWAL,
    WITHDRAWAL_REQUEST,
    WITHDRAWAL_COMPLETED,
    TRANSFER,
    MILESTONE_RELEASE
}
