using Aivora.Repositories.Enums;

namespace Aivora.Services.FinancialLedger;

public interface IFinancialLedger
{
    /// <summary>
    /// Moves funds from Payer's AvailableBalance to HeldBalance for a specific milestone.
    /// </summary>
    Task EscrowFundsAsync(Guid payerId, Guid milestoneId, decimal amount, string description);

    /// <summary>
    /// Releases HELD funds from Payer's HeldBalance to Payee's AvailableBalance.
    /// </summary>
    Task ReleaseFundsAsync(Guid milestoneId, decimal amount, string description);

    /// <summary>
    /// Refunds HELD funds from Payer's HeldBalance back to Payer's AvailableBalance.
    /// </summary>
    Task RefundFundsAsync(Guid milestoneId, decimal amount, string description);

    /// <summary>
    /// Splits HELD funds between Payee's AvailableBalance and Payer's AvailableBalance.
    /// </summary>
    Task SplitFundsAsync(Guid milestoneId, decimal releaseAmount, decimal refundAmount, string description);
}
