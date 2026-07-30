using System.Text.Json;

namespace Aivora.Services.AIMilestoneStepAssistantService.Prompting;

public class AIMilestoneStepSuggestionPromptBuilder
{
    public string Build(Request.SuggestMilestoneStepsRequest request)
    {
        var context = new
        {
            request.Title,
            request.Description,
            request.AcceptanceCriteria
        };

        return $$"""
            You are an AI assistant helping a freelance Expert plan their work.
            Break the following Milestone down into a short, ordered list of concrete work steps.
            Milestone details:
            {{JsonSerializer.Serialize(context)}}

            Return ONLY one JSON object with this schema:
            {
              "steps": [
                {
                  "title": "concise step title",
                  "description": "what the step involves",
                  "estimatedDays": 2
                }
              ]
            }
            """;
    }

    // Mirrors the JSON template above. "title" is required per item — the parser drops any
    // step whose title is missing rather than falling back to a placeholder.
    public static object ResponseSchema { get; } = new
    {
        type = "OBJECT",
        properties = new
        {
            steps = new
            {
                type = "ARRAY",
                items = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        title = new { type = "STRING" },
                        description = new { type = "STRING" },
                        estimatedDays = new { type = "INTEGER" }
                    },
                    required = new[] { "title" }
                }
            }
        }
    };
}
