using Aivora.Services.Options;
using Microsoft.Extensions.Options;

namespace Aivora.Services.Treasury;

public class CommissionCalculator : ICommissionCalculator
{
    private readonly CommissionOptions _options;

    public CommissionCalculator(IOptions<CommissionOptions> options)
    {
        _options = options.Value;
    }

    public decimal CalculateCommission(decimal milestoneAmount)
    {
        return milestoneAmount * _options.Rate;
    }

    public decimal MaxDebtLimit => _options.MaxDebtLimit;

    public decimal Rate => _options.Rate;
}
