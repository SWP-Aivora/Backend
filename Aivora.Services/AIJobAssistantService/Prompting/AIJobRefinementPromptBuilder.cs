using System.Text.Json;

namespace Aivora.Services.AIJobAssistantService.Prompting;

public class AIJobRefinementPromptBuilder
{
    public string Build(Response.SuggestionResponse current, string message)
    {
        return $$"""
            You are an AI Job Assistant. Refine or explain this current job suggestion:
            {{JsonSerializer.Serialize(current)}}

            User message:
            {{message}}

            Return ONLY one JSON object with these fields:
            {
              "updatedSuggestion": {
                "suggestedTitle": "title",
                "suggestedDescription": "description",
                "businessDomain": "domain",
                "expectedOutcome": "outcome",
                "budgetType": "FIXED or HOURLY",
                "currency": "AICOIN, USD, or VND",
                "suggestedBudgetMin": 500,
                "suggestedBudgetMax": 1500,
                "suggestedTimelineDays": 14,
                "experienceLevel": "BEGINNER, INTERMEDIATE, ADVANCED, or EXPERT",
                "suggestedSkills": ["skill"],
                "suggestedMilestones": [],
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
}
