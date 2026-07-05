namespace Aivora.Services.Treasury;

public interface ICommissionCalculator
{
    decimal CalculateCommission(decimal milestoneAmount);

    /// <summary>
    /// Maximum debt allowed per expert wallet. 0 = unlimited.
    /// </summary>
    decimal MaxDebtLimit { get; }
}
