using System.Text.Json;
using Aivora.Services.AIJobAssistantService.Parsing;

namespace Aivora.Services.AIMilestoneStepAssistantService.Parsing;

public class AIMilestoneStepSuggestionParser
{
    public AIMilestoneStepSuggestionDraft Parse(string providerText, Request.SuggestMilestoneStepsRequest request)
    {
        using var document = AIJsonParser.ParseObject(providerText);
        var fallback = BuildFallbackDraft(request);

        var steps = ReadSteps(document.RootElement);
        return new AIMilestoneStepSuggestionDraft
        {
            Steps = steps.Count > 0 ? steps : fallback.Steps,
            AIModel = "Gemini 2.5 Flash"
        };
    }

    private static AIMilestoneStepSuggestionDraft BuildFallbackDraft(Request.SuggestMilestoneStepsRequest request)
    {
        return new AIMilestoneStepSuggestionDraft
        {
            Steps = new List<Response.SuggestedStep>
            {
                new() { Title = $"Plan: {request.Title}", Description = "Break down the requirements and confirm scope before starting work.", EstimatedDays = 2 },
                new() { Title = "Implement core work", Description = request.Description ?? "Carry out the main body of work described in the milestone.", EstimatedDays = 5 },
                new() { Title = "Review against acceptance criteria", Description = request.AcceptanceCriteria ?? "Verify the completed work meets the milestone's acceptance criteria.", EstimatedDays = 1 }
            },
            AIModel = "Gemini 2.5 Flash"
        };
    }

    private static List<Response.SuggestedStep> ReadSteps(JsonElement element)
    {
        if (!AIJsonParser.TryGetProperty(element, "steps", out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return new List<Response.SuggestedStep>();
        }

        var steps = new List<Response.SuggestedStep>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var title = AIJsonParser.GetString(item, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            steps.Add(new Response.SuggestedStep
            {
                Title = title,
                Description = AIJsonParser.GetString(item, "description"),
                EstimatedDays = AIJsonParser.GetInt(item, "estimatedDays") ?? 0
            });
        }

        return steps;
    }
}
