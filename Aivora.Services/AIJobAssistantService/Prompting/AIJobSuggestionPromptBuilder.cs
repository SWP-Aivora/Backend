using System.Text.Json;
using Aivora.Repositories.Enums;

namespace Aivora.Services.AIJobAssistantService.Prompting;

public class AIJobSuggestionPromptBuilder
{
    public string Build(Request.GenerateSuggestionRequest request)
    {
        var context = new
        {
            request.RawInput,
            request.BusinessDomain,
            request.ExpectedOutcome,
            request.BudgetType,
            request.Currency,
            request.BudgetMin,
            request.BudgetMax,
            request.TimelineDays,
            request.ExperienceLevel,
            AvailableCategories = request.CategoriesContext
        };

        return $$"""
            You are an AI Job Assistant for a freelance marketplace.
            Parse the client requirement into a structured job suggestion in English.
            Client input and hints:
            {{JsonSerializer.Serialize(context)}}

            Return ONLY one JSON object with this schema:
            {
              "suggestedTitle": "concise job title",
              "suggestedDescription": "official detailed job description",
              "categoryName": "exact name of the closest matching category",
              "businessDomain": "business vertical",
              "expectedOutcome": "project outcome",
              "budgetType": "FIXED or HOURLY",
              "suggestedBudgetMin": 500,
              "suggestedBudgetMax": 1500,
              "currency": "AICOIN, USD, or VND",
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
            }
            """;
    }

    // Mirrors the JSON template above so the Gemini call in GeminiAIJobSuggestionProvider can
    // pass it as responseSchema — nothing marked required since AIJsonParser.ParseSuggestionDraft
    // falls back to sane defaults for every field.
    public static object ResponseSchema { get; } = new
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
            suggestedBudgetMin = new { type = "NUMBER" },
            suggestedBudgetMax = new { type = "NUMBER" },
            currency = new { type = "STRING", @enum = new[] { "AICOIN", "USD", "VND" } },
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
    };
}
