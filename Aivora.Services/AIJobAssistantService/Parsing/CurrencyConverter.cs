using Aivora.Services.Exceptions;

namespace Aivora.Services.AIJobAssistantService.Parsing;

public static class CurrencyConverter
{
    public const string BaseCurrency = "AICOIN";

    public static (string Currency, decimal? BudgetMin, decimal? BudgetMax, List<Response.SuggestedMilestone> Milestones)
        ConvertToAicoin(
            string? currency,
            decimal? budgetMin,
            decimal? budgetMax,
            List<Response.SuggestedMilestone> milestones,
            IReadOnlyDictionary<string, decimal> ratesToAicoin)
    {
        var normalized = AIJsonParser.NormalizeCurrency(currency);
        if (normalized == BaseCurrency)
        {
            return (normalized, budgetMin, budgetMax, milestones);
        }

        // Unknown currency (outside the configured rate table, e.g. AI hallucinated "EUR" or a typo):
        // reject rather than silently default to rate 1 — that would misconvert real money
        // (e.g. 100 EUR would be stored as 100 AICOIN instead of ~2700 AICOIN).
        if (!ratesToAicoin.TryGetValue(normalized, out var rate))
        {
            throw new ValidationException($"Unsupported currency '{normalized}'. No exchange rate is configured for it.");
        }

        var converted = milestones.Select(m => new Response.SuggestedMilestone
        {
            Title = m.Title,
            Description = m.Description,
            Amount = m.Amount * rate,
            DueDays = m.DueDays,
            AcceptanceCriteria = m.AcceptanceCriteria
        }).ToList();

        return (BaseCurrency, budgetMin * rate, budgetMax * rate, converted);
    }
}
