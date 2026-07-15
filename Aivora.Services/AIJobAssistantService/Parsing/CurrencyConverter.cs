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

        // Unknown currency (AI response outside AICOIN/USD/VND): default rate 1 (amount unchanged)
        // rather than throwing — a stray currency label shouldn't fail the whole request.
        var rate = ratesToAicoin.TryGetValue(normalized, out var r) ? r : 1m;

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
