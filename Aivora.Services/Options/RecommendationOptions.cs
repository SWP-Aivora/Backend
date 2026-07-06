using System.ComponentModel.DataAnnotations;

namespace Aivora.Services.Options;

public class RecommendationOptions
{
    [Range(0.0, 5.0, ErrorMessage = "Dispute penalty factor must be non-negative")]
    public decimal DisputePenaltyFactor { get; set; } = 1.5m;

    [Range(0.0, 1.0, ErrorMessage = "Max dispute penalty must be between 0.0 and 1.0")]
    public decimal MaxDisputePenalty { get; set; } = 0.5m;

    [Range(0.0, 5.0, ErrorMessage = "Overdue penalty factor must be non-negative")]
    public decimal OverduePenaltyFactor { get; set; } = 0.3m;

    [Range(0.0, 1.0, ErrorMessage = "Max overdue penalty must be between 0.0 and 1.0")]
    public decimal MaxOverduePenalty { get; set; } = 0.3m;
}
