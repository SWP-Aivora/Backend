using Aivora.Repositories.Enums;
using Aivora.Services.AIJobRefinementService.Parsing;
using FluentAssertions;
using Xunit;

namespace Aivora.Tests.Services;

public class AIJobRefinementParserTests
{
    [Fact]
    public void Parse_WithEmptyChangedFieldsSelfReport_AppliesUpdate()
    {
        // Regression test: Gemini's own "changedFields" self-report is not trustworthy (it can
        // omit a field it actually changed). The parser must always apply "updatedJob" and let
        // the caller diff it against the current state — not discard real edits just because
        // the AI forgot to list them.
        var parser = new AIJobRefinementParser();
        var draft = parser.Parse(
            """
            {
              "updatedJob": {
                "budgetMin": 2000,
                "budgetMax": 3000
              },
              "aiResponse": "Da cap nhat budget.",
              "changedFields": []
            }
            """,
            BuildCurrentJob());

        draft.BudgetMin.Should().Be(2000);
        draft.BudgetMax.Should().Be(3000);
    }

    private static Aivora.Services.JobService.Response.JobResponse BuildCurrentJob()
    {
        return new Aivora.Services.JobService.Response.JobResponse
        {
            Id = Guid.NewGuid(),
            Title = "Current job",
            OriginalDescription = "Current description",
            ClientId = Guid.NewGuid(),
            ClientName = "Client",
            BudgetType = BudgetType.FIXED,
            Currency = "AICOIN",
            BudgetMin = 500,
            BudgetMax = 1000,
            Status = JobStatus.DRAFT,
            Visibility = JobVisibility.PRIVATE,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
