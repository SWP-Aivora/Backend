namespace Aivora.Services.Treasury;

public interface ICommissionCalculator
{
    decimal CalculateCommission(decimal milestoneAmount);
}
