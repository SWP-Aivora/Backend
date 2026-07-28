using System.ComponentModel.DataAnnotations;

namespace Aivora.Services.Options;

public class RecommendationOptions : IValidatableObject
{
    [Range(0.0, 5.0, ErrorMessage = "Dispute penalty factor must be non-negative")]
    public decimal DisputePenaltyFactor { get; set; } = 1.5m;

    [Range(0.0, 1.0, ErrorMessage = "Max dispute penalty must be between 0.0 and 1.0")]
    public decimal MaxDisputePenalty { get; set; } = 0.5m;

    [Range(0.0, 5.0, ErrorMessage = "Overdue penalty factor must be non-negative")]
    public decimal OverduePenaltyFactor { get; set; } = 0.3m;

    [Range(0.0, 1.0, ErrorMessage = "Max overdue penalty must be between 0.0 and 1.0")]
    public decimal MaxOverduePenalty { get; set; } = 0.3m;

    [Range(0.0, 1.0, ErrorMessage = "Skill weight must be between 0.0 and 1.0")]
    public decimal SkillWeight { get; set; } = 0.40m;

    [Range(0.0, 1.0, ErrorMessage = "Budget weight must be between 0.0 and 1.0")]
    public decimal BudgetWeight { get; set; } = 0.20m;

    [Range(0.0, 1.0, ErrorMessage = "Rating weight must be between 0.0 and 1.0")]
    public decimal RatingWeight { get; set; } = 0.20m;

    [Range(0.0, 1.0, ErrorMessage = "Availability weight must be between 0.0 and 1.0")]
    public decimal AvailabilityWeight { get; set; } = 0.10m;

    [Range(0.0, 1.0, ErrorMessage = "Completion weight must be between 0.0 and 1.0")]
    public decimal CompletionWeight { get; set; } = 0.10m;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var total = SkillWeight + BudgetWeight + RatingWeight + AvailabilityWeight + CompletionWeight;
        if (Math.Round(total, 4) != 1.0m)
        {
            yield return new ValidationResult(
                $"SkillWeight + BudgetWeight + RatingWeight + AvailabilityWeight + CompletionWeight must sum to 1.0 (got {total}).");
        }
    }
}
