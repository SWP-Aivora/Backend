using Aivora.Repositories.Abstractions;
using Aivora.Repositories.Enums;

namespace Aivora.Repositories.Entities;

public class AIJobSuggestion : AuditableBaseEntity
{
    public Guid? JobId { get; set; }
    public Guid ClientId { get; set; }
    public string RawInput { get; set; } = null!;
    public string? SuggestedTitle { get; set; }
    public string? SuggestedDescription { get; set; }
    public decimal? SuggestedBudgetMin { get; set; }
    public decimal? SuggestedBudgetMax { get; set; }
    public int? SuggestedTimelineDays { get; set; }
    
    // JSON data stored as strings
    public string? SuggestedSkillsJson { get; set; }
    public string? SuggestedMilestonesJson { get; set; }
    public string? ClarifyingQuestionsJson { get; set; }
    public string? RiskWarningsJson { get; set; }

    public string? AIModel { get; set; }
    public AIJobSuggestionStatus Status { get; set; } = AIJobSuggestionStatus.GENERATED;

    // Navigation Properties
    public virtual User Client { get; set; } = null!;
    public virtual JobPost? Job { get; set; }
}

