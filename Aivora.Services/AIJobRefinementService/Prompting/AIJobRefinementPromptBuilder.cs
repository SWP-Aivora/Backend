using System.Text.Json;

namespace Aivora.Services.AIJobRefinementService.Prompting;

public class AIJobRefinementPromptBuilder
{
    public string Build(JobService.Response.JobResponse current, string message)
    {
        return $$"""
            You are an AI Job Assistant. Refine this existing job post based on the user's request:
            {{JsonSerializer.Serialize(current)}}

            User message:
            {{message}}

            Return ONLY one JSON object with these fields:
            {
              "updatedJob": {
                "title": "title",
                "finalDescription": "description",
                "businessDomain": "domain",
                "expectedOutcome": "outcome",
                "budgetType": "FIXED or HOURLY",
                "currency": "AICOIN, USD, or VND",
                "budgetMin": 500,
                "budgetMax": 1500,
                "timelineDays": 14,
                "experienceLevel": "BEGINNER, INTERMEDIATE, ADVANCED, or EXPERT",
                "skills": ["skill1"],
                "milestones": []
              },
              "aiResponse": "friendly response",
              "changedFields": ["camelCaseFieldName"]
            }

            If the user is asking for advice or an explanation, keep updatedJob unchanged and return an empty changedFields array.
            """;
    }
}
