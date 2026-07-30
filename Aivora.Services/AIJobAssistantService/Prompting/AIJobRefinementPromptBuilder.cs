using System.Text.Json;
using Aivora.Repositories.Enums;

namespace Aivora.Services.AIJobAssistantService.Prompting;

public class AIJobRefinementPromptBuilder
{
    public string Build(Response.SuggestionResponse current, Request.RefineSuggestionRequest request)
    {
        return $$"""
            You are an AI Job Assistant. Refine or explain this current job suggestion. Important: You MUST generate all responses and text content strictly in English, regardless of the language the user uses:
            {{JsonSerializer.Serialize(current)}}

            User message:
            {{request.Message}}

            Available categories:
            {{request.CategoriesContext}}

            Return ONLY one JSON object with these fields:
            {
              "updatedSuggestion": {
                "suggestedTitle": "title",
                "suggestedDescription": "description",
                "categoryName": "exact name of the closest matching category",
                "businessDomain": "domain",
                "expectedOutcome": "outcome",
                "budgetType": "FIXED or HOURLY",
                "currency": "AICOIN, USD, or VND",
                "suggestedBudgetMin": 500,
                "suggestedBudgetMax": 1500,
                "suggestedTimelineDays": 14,
                "experienceLevel": "BEGINNER, INTERMEDIATE, ADVANCED, or EXPERT",
                "suggestedSkills": ["skill"],
                "suggestedMilestones": [
                  {
                    "title": "milestone title",
                    "description": "milestone description",
                    "amount": 20,
                    "dueDays": 3,
                    "acceptanceCriteria": "acceptance criteria"
                  }
                ],
                "clarifyingQuestions": ["question"],
                "clarifyingAnswers": [""],
                "riskWarnings": ["risk"]
              },
              "aiResponse": "friendly response",
              "changedFields": ["camelCaseFieldName"]
            }

            If the user is asking for advice or an explanation, keep updatedSuggestion unchanged and return an empty changedFields array.
            """;
    }

    // Mirrors the JSON template above. Nothing is required at the outer level: the Service
    // itself diffs updatedSuggestion against the current entity, so an AI that echoes back
    // unchanged/empty fields (advisory-only message) is valid, not an error.
    public static object ResponseSchema { get; } = new
    {
        type = "OBJECT",
        properties = new
        {
            updatedSuggestion = new
            {
                type = "OBJECT",
                properties = new
                {
                    suggestedTitle = new { type = "STRING" },
                    suggestedDescription = new { type = "STRING" },
                    categoryName = new { type = "STRING" },
                    businessDomain = new { type = "STRING" },
                    expectedOutcome = new { type = "STRING" },
                    budgetType = new { type = "STRING", @enum = Enum.GetNames<BudgetType>() },
                    currency = new { type = "STRING", @enum = new[] { "AICOIN", "USD", "VND" } },
                    suggestedBudgetMin = new { type = "NUMBER" },
                    suggestedBudgetMax = new { type = "NUMBER" },
                    suggestedTimelineDays = new { type = "INTEGER" },
                    experienceLevel = new { type = "STRING", @enum = Enum.GetNames<SkillLevel>() },
                    suggestedSkills = new { type = "ARRAY", items = new { type = "STRING" } },
                    suggestedMilestones = new
                    {
                        type = "ARRAY",
                        items = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                title = new { type = "STRING" },
                                description = new { type = "STRING" },
                                amount = new { type = "NUMBER" },
                                dueDays = new { type = "INTEGER" },
                                acceptanceCriteria = new { type = "STRING" }
                            }
                        }
                    },
                    clarifyingQuestions = new { type = "ARRAY", items = new { type = "STRING" } },
                    clarifyingAnswers = new { type = "ARRAY", items = new { type = "STRING" } },
                    riskWarnings = new { type = "ARRAY", items = new { type = "STRING" } }
                }
            },
            aiResponse = new { type = "STRING" },
            changedFields = new { type = "ARRAY", items = new { type = "STRING" } }
        }
    };
}
