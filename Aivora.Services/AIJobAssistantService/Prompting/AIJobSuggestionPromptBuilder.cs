using System.Text.Json;

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
              "riskWarnings": ["risk"]
            }
            """;
    }
}
