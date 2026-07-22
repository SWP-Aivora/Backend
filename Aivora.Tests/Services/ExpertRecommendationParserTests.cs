using Aivora.Services.RecommendationService;
using Aivora.Services.RecommendationService.Parsing;
using FluentAssertions;
using Xunit;

namespace Aivora.Tests.Services;

public class ExpertRecommendationParserTests
{
    private readonly ExpertRecommendationParser _parser = new();

    [Fact]
    public void Parse_FiltersInvalidIds_DedupesAndCapsAtFive_PreservesAiOrder()
    {
        var candidates = CreateCandidates(7);
        var context = CreateContext(candidates);
        var unknownId = Guid.NewGuid();

        var providerText = $$"""
            {
              "ranked": [
                { "expertId": "{{candidates[2].ExpertId}}", "reasoning": "third" },
                { "expertId": "{{candidates[0].ExpertId}}", "reasoning": "first" },
                { "expertId": "{{unknownId}}", "reasoning": "not a real candidate" },
                { "expertId": "{{candidates[0].ExpertId}}", "reasoning": "duplicate of first" },
                { "expertId": "{{candidates[4].ExpertId}}", "reasoning": "fifth" },
                { "expertId": "{{candidates[1].ExpertId}}", "reasoning": "second" },
                { "expertId": "{{candidates[3].ExpertId}}", "reasoning": "fourth" },
                { "expertId": "{{candidates[6].ExpertId}}", "reasoning": "seventh, beyond cap" }
              ]
            }
            """;

        var draft = _parser.Parse(providerText, context);

        draft.Ranked.Should().HaveCount(5);
        draft.Ranked.Select(r => r.ExpertId).Should().Equal(
            candidates[2].ExpertId,
            candidates[0].ExpertId,
            candidates[4].ExpertId,
            candidates[1].ExpertId,
            candidates[3].ExpertId);
        draft.AIModel.Should().Be("Gemini 2.5 Flash");
    }

    [Fact]
    public void Parse_MissingRankedProperty_FallsBackToScorerOrder()
    {
        var candidates = CreateCandidates(3);
        var context = CreateContext(candidates);

        var draft = _parser.Parse("{}", context);

        draft.Ranked.Select(r => r.ExpertId).Should().Equal(candidates.Select(c => c.ExpertId));
        draft.Ranked.Select(r => r.Reasoning).Should().Equal(candidates.Select(c => c.ScorerExplanation));
    }

    [Fact]
    public void Parse_EmptyRankedArray_FallsBackToScorerOrder()
    {
        var candidates = CreateCandidates(3);
        var context = CreateContext(candidates);

        var draft = _parser.Parse("""{ "ranked": [] }""", context);

        draft.Ranked.Select(r => r.ExpertId).Should().Equal(candidates.Select(c => c.ExpertId));
    }

    [Fact]
    public void Parse_TruncatesReasoningOver2000Chars()
    {
        var candidates = CreateCandidates(1);
        var context = CreateContext(candidates);
        var longReasoning = new string('a', 2500);

        var providerText = $$"""
            { "ranked": [ { "expertId": "{{candidates[0].ExpertId}}", "reasoning": "{{longReasoning}}" } ] }
            """;

        var draft = _parser.Parse(providerText, context);

        draft.Ranked.Single().Reasoning.Length.Should().Be(2000);
    }

    private static List<CandidateExpert> CreateCandidates(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new CandidateExpert
            {
                ExpertId = Guid.NewGuid(),
                Skills = new List<string> { "React" },
                Rating = 4.5m,
                HourlyRate = 25,
                AvailabilityStatus = "AVAILABLE",
                SuccessRate = 90,
                CompletedProjects = 10,
                DisputeCount = 0,
                OverdueRate = 0,
                ScorerTotalScore = 100 - i,
                ScorerExplanation = $"Scorer explanation for candidate {i}."
            })
            .ToList();
    }

    private static ExpertRecommendationContext CreateContext(List<CandidateExpert> candidates)
    {
        return new ExpertRecommendationContext
        {
            JobTitle = "Build a chatbot",
            JobDescription = "Need an AI chatbot for customer support.",
            RequiredSkills = new List<string> { "React" },
            BudgetType = "FIXED",
            BudgetMin = 500,
            BudgetMax = 1500,
            Candidates = candidates
        };
    }
}
