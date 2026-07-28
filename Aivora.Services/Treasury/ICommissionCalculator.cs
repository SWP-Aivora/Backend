namespace Aivora.Services.Treasury;

public interface ICommissionCalculator
{
    decimal CalculateCommission(decimal milestoneAmount);

    /// <summary>
    /// Maximum debt allowed per expert wallet. 0 = unlimited.
    /// </summary>
    decimal MaxDebtLimit { get; }

    /// <summary>
    /// Current commission rate (0.0-1.0), for display purposes.
    /// </summary>
    decimal Rate { get; }
}
