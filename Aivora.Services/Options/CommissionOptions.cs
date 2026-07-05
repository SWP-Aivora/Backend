using System.ComponentModel.DataAnnotations;

namespace Aivora.Services.Options;

public class CommissionOptions
{
    [Range(0.0, 1.0, ErrorMessage = "Commission rate must be between 0.0 and 1.0")]
    public decimal Rate { get; set; } = 0.10m;

    /// <summary>
    /// Maximum debt an expert wallet can accumulate from clawbacks before refunds are blocked.
    /// 0 means unlimited. Default: 1000 AICOIN.
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "Max debt limit must be non-negative")]
    public decimal MaxDebtLimit { get; set; } = 1000m;
}
