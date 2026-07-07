using Aivora.Services.AIMilestoneStepAssistantService;
using Aivora.Services.AIMilestoneStepAssistantService.Parsing;
using Aivora.Services.AIMilestoneStepAssistantService.Providers;
using Aivora.Services.Exceptions;
using FluentAssertions;
using Xunit;

namespace Aivora.Tests.Services;

public class AIMilestoneStepSuggestionProviderTests
{
    [Fact]
    public async Task MockProvider_ReturnsDeterministicNonEmptySteps()
    {
        var provider = new MockAIMilestoneStepSuggestionProvider();
        var request = new Request.SuggestMilestoneStepsRequest
        {
            Title = "Build a chatbot widget",
            Description = "Embeddable chat widget for the marketing site",
            AcceptanceCriteria = "Widget loads under 200ms and responds to basic FAQs"
        };

        var draft = await provider.GenerateSuggestionAsync(request);

        draft.Steps.Should().NotBeEmpty();
        draft.Steps.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Title));
        draft.AIModel.Should().Be("Aivora-Mock");
    }

    [Fact]
    public void Parser_ParsesValidStepsArray()
    {
        var parser = new AIMilestoneStepSuggestionParser();
        var draft = parser.Parse(
            """
            {
              "steps": [
                { "title": "Design schema", "description": "Draft the DB schema" },
                { "title": "Build API", "description": "Implement the endpoints" }
              ]
            }
            """,
            new Request.SuggestMilestoneStepsRequest { Title = "Build API", Description = "desc", AcceptanceCriteria = "criteria" });

        draft.Steps.Should().HaveCount(2);
        draft.Steps[0].Title.Should().Be("Design schema");
        draft.AIModel.Should().Be("Gemini 2.5 Flash");
    }

    [Fact]
    public void Parser_StripsMarkdownJsonFence()
    {
        var parser = new AIMilestoneStepSuggestionParser();
        var draft = parser.Parse(
            """
            ```json
            { "steps": [ { "title": "Fenced step" } ] }
            ```
            """,
            new Request.SuggestMilestoneStepsRequest { Title = "Job", Description = "desc", AcceptanceCriteria = "criteria" });

        draft.Steps.Should().ContainSingle(s => s.Title == "Fenced step");
    }

    [Fact]
    public void Parser_MissingStepsArray_FallsBackWithoutThrowing()
    {
        var parser = new AIMilestoneStepSuggestionParser();
        var draft = parser.Parse(
            """
            { "notes": "no steps key here" }
            """,
            new Request.SuggestMilestoneStepsRequest { Title = "Fallback job", Description = "desc", AcceptanceCriteria = "criteria" });

        draft.Steps.Should().NotBeEmpty();
    }

    [Fact]
    public void Parser_EmptyResponse_ThrowsValidationException()
    {
        var parser = new AIMilestoneStepSuggestionParser();

        Action act = () => parser.Parse("", new Request.SuggestMilestoneStepsRequest { Title = "Job" });

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Parser_NonJsonResponse_ThrowsValidationExceptionNotUnhandled()
    {
        var parser = new AIMilestoneStepSuggestionParser();

        Action act = () => parser.Parse("The AI just wrote plain prose with no JSON object at all.", new Request.SuggestMilestoneStepsRequest { Title = "Job" });

        act.Should().Throw<ValidationException>();
    }
}
