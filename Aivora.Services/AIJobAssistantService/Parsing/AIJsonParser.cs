using System.Globalization;
using System.Text.Json;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;

namespace Aivora.Services.AIJobAssistantService.Parsing;

internal static class AIJsonParser
{
    public static JsonDocument ParseObject(string providerText)
    {
        var json = ExtractJsonObject(providerText);
        return JsonDocument.Parse(json);
    }

    public static AIJobSuggestionDraft ParseSuggestionDraft(JsonElement element, AIJobSuggestionDraft fallback)
    {
        var questions = ReadStringList(element, "clarifyingQuestions");
        if (questions.Count == 0)
        {
            questions = fallback.ClarifyingQuestions.ToList();
        }

        var answers = ReadStringList(element, "clarifyingAnswers");
        if (answers.Count == 0)
        {
            answers = fallback.ClarifyingAnswers.ToList();
        }

        while (answers.Count < questions.Count)
        {
            answers.Add(string.Empty);
        }

        return new AIJobSuggestionDraft
        {
            SuggestedTitle = GetString(element, "suggestedTitle") ?? fallback.SuggestedTitle,
            SuggestedDescription = GetString(element, "suggestedDescription") ?? fallback.SuggestedDescription,
            BusinessDomain = GetString(element, "businessDomain") ?? GetString(element, "suggestedBusinessDomain") ?? fallback.BusinessDomain,
            ExpectedOutcome = GetString(element, "expectedOutcome") ?? GetString(element, "suggestedExpectedOutcome") ?? fallback.ExpectedOutcome,
            BudgetType = GetBudgetType(element) ?? fallback.BudgetType,
            Currency = NormalizeCurrency(GetString(element, "currency") ?? fallback.Currency),
            SuggestedBudgetMin = GetDecimal(element, "suggestedBudgetMin") ?? GetDecimal(element, "budgetMin") ?? fallback.SuggestedBudgetMin,
            SuggestedBudgetMax = GetDecimal(element, "suggestedBudgetMax") ?? GetDecimal(element, "budgetMax") ?? fallback.SuggestedBudgetMax,
            SuggestedTimelineDays = GetInt(element, "suggestedTimelineDays") ?? GetInt(element, "timelineDays") ?? fallback.SuggestedTimelineDays,
            ExperienceLevel = GetSkillLevel(element) ?? fallback.ExperienceLevel,
            SuggestedSkills = ReadStringList(element, "suggestedSkills", fallback.SuggestedSkills),
            SuggestedMilestones = ReadMilestones(element, fallback.SuggestedMilestones),
            ClarifyingQuestions = questions,
            ClarifyingAnswers = answers,
            RiskWarnings = ReadStringList(element, "riskWarnings", fallback.RiskWarnings),
            AIModel = fallback.AIModel
        };
    }

    public static string? GetString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrWhiteSpace(property.GetString()) ? null : property.GetString()!.Trim(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    public static decimal? GetDecimal(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String
            && decimal.TryParse(property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    public static int? GetInt(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        if (property.ValueKind == JsonValueKind.String
            && int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    public static List<string> ReadStringList(JsonElement element, string name, IEnumerable<string>? fallback = null)
    {
        if (!TryGetProperty(element, name, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return fallback?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList() ?? new List<string>();
        }

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            string? value = null;
            if (item.ValueKind == JsonValueKind.String)
            {
                value = item.GetString();
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                value = GetString(item, "question") ?? GetString(item, "label") ?? GetString(item, "value");
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value.Trim());
            }
        }

        return values.Count > 0
            ? values
            : fallback?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList() ?? new List<string>();
    }

    public static List<Response.SuggestedMilestone> ReadMilestones(JsonElement element, IEnumerable<Response.SuggestedMilestone>? fallback = null)
    {
        if (!TryGetProperty(element, "suggestedMilestones", out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return fallback?.Select(CloneMilestone).ToList() ?? new List<Response.SuggestedMilestone>();
        }

        var milestones = new List<Response.SuggestedMilestone>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var title = GetString(item, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            milestones.Add(new Response.SuggestedMilestone
            {
                Title = title,
                Description = GetString(item, "description"),
                Amount = GetDecimal(item, "amount") ?? 0,
                DueDays = GetInt(item, "dueDays") ?? 0,
                AcceptanceCriteria = GetString(item, "acceptanceCriteria")
            });
        }

        return milestones.Count > 0
            ? milestones
            : fallback?.Select(CloneMilestone).ToList() ?? new List<Response.SuggestedMilestone>();
    }

    public static BudgetType? GetBudgetType(JsonElement element)
    {
        var value = GetString(element, "budgetType") ?? GetString(element, "suggestedBudgetType");
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<BudgetType>(value.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    public static SkillLevel? GetSkillLevel(JsonElement element)
    {
        var value = GetString(element, "experienceLevel") ?? GetString(element, "suggestedExperienceLevel");
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NormalizeSkillLevel(value);
    }

    public static SkillLevel? NormalizeSkillLevel(string value)
    {
        return value.Trim().ToUpperInvariant() switch
        {
            "ENTRY" => SkillLevel.BEGINNER,
            "JUNIOR" => SkillLevel.BEGINNER,
            "BEGINNER" => SkillLevel.BEGINNER,
            "INTERMEDIATE" => SkillLevel.INTERMEDIATE,
            "MID" => SkillLevel.INTERMEDIATE,
            "ADVANCED" => SkillLevel.ADVANCED,
            "SENIOR" => SkillLevel.ADVANCED,
            "EXPERT" => SkillLevel.EXPERT,
            _ => Enum.TryParse<SkillLevel>(value.Trim(), ignoreCase: true, out var parsed) ? parsed : null
        };
    }

    public static string NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return "AICOIN";
        }

        var normalized = currency.Trim().ToUpperInvariant();
        if (normalized.Length > 10)
        {
            throw new ValidationException("Currency must be 10 characters or fewer.");
        }

        return normalized;
    }

    public static bool TryGetProperty(JsonElement element, string name, out JsonElement property)
    {
        foreach (var jsonProperty in element.EnumerateObject())
        {
            if (string.Equals(jsonProperty.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = jsonProperty.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private static string ExtractJsonObject(string providerText)
    {
        if (string.IsNullOrWhiteSpace(providerText))
        {
            throw new ValidationException("AI provider returned an empty response.");
        }

        var trimmed = providerText.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new ValidationException("AI provider response did not contain a JSON object.");
        }

        return trimmed[start..(end + 1)];
    }

    private static Response.SuggestedMilestone CloneMilestone(Response.SuggestedMilestone milestone)
    {
        return new Response.SuggestedMilestone
        {
            Title = milestone.Title,
            Description = milestone.Description,
            Amount = milestone.Amount,
            DueDays = milestone.DueDays,
            AcceptanceCriteria = milestone.AcceptanceCriteria
        };
    }
}
