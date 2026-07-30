using System.Text.Json;
using Aivora.Repositories.Enums;

namespace Aivora.Services.AIJobRefinementService.Prompting;

public class AIJobRefinementPromptBuilder
{
    public string Build(JobService.Response.JobResponse current, string message)
    {
        return $$"""
            You are an AI Job Assistant. Refine this existing job post based on the user's request. Important: You MUST generate all responses and text content strictly in English, regardless of the language the user uses:
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
                "milestones": [
                  {
                    "title": "milestone title",
                    "description": "milestone description",
                    "amount": 20,
                    "dueDays": 3,
                    "acceptanceCriteria": "acceptance criteria"
                  }
                ]
              },
              "aiResponse": "friendly response",
              "changedFields": ["camelCaseFieldName"]
            }

            If the user is asking for advice or an explanation, keep updatedJob unchanged and return an empty changedFields array.
            """;
    }

    // Mirrors the JSON template above. Nothing required: every field in ParseJobDraft falls
    // back to the current job's value, so an advisory-only reply that omits/echoes fields is valid.
    public static object ResponseSchema { get; } = new
    {
        type = "OBJECT",
        properties = new
        {
            updatedJob = new
            {
                type = "OBJECT",
                properties = new
                {
                    title = new { type = "STRING" },
                    finalDescription = new { type = "STRING" },
                    businessDomain = new { type = "STRING" },
                    expectedOutcome = new { type = "STRING" },
                    budgetType = new { type = "STRING", @enum = Enum.GetNames<BudgetType>() },
                    currency = new { type = "STRING", @enum = new[] { "AICOIN", "USD", "VND" } },
                    budgetMin = new { type = "NUMBER" },
                    budgetMax = new { type = "NUMBER" },
                    timelineDays = new { type = "INTEGER" },
                    experienceLevel = new { type = "STRING", @enum = Enum.GetNames<SkillLevel>() },
                    skills = new { type = "ARRAY", items = new { type = "STRING" } },
                    milestones = new
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
                    }
                }
            },
            aiResponse = new { type = "STRING" },
            changedFields = new { type = "ARRAY", items = new { type = "STRING" } }
        }
    };
}
