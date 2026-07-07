namespace Aivora.Services.AIMilestoneStepAssistantService.Providers;

public class MockAIMilestoneStepSuggestionProvider : IAIMilestoneStepSuggestionProvider
{
    public Task<AIMilestoneStepSuggestionDraft> GenerateSuggestionAsync(Request.SuggestMilestoneStepsRequest request, CancellationToken cancellationToken = default)
    {
        var draft = new AIMilestoneStepSuggestionDraft
        {
            Steps = new List<Response.SuggestedStep>
            {
                new() { Title = $"Plan: {request.Title}", Description = "Break down the requirements and confirm scope before starting work." },
                new() { Title = "Implement core work", Description = request.Description ?? "Carry out the main body of work described in the milestone." },
                new() { Title = "Review against acceptance criteria", Description = request.AcceptanceCriteria ?? "Verify the completed work meets the milestone's acceptance criteria." }
            },
            AIModel = "Aivora-Mock"
        };

        return Task.FromResult(draft);
    }
}
