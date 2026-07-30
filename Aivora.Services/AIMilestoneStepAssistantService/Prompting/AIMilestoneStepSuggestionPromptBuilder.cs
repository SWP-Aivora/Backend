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
                  "estimatedDays": "estimated number of days to complete this step"
                }
              ]
            }
            """;
    }
}
