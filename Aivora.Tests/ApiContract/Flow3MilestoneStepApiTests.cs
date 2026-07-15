using System.Net;
using Aivora.Repositories.Enums;
using FluentAssertions;
using Xunit;

namespace Aivora.Tests.ApiContract;

[Collection("ApiContract")]
public class Flow3MilestoneStepApiTests : IClassFixture<ApiContractTestFactory>
{
    private readonly ApiContractTestFactory _factory;

    public Flow3MilestoneStepApiTests(ApiContractTestFactory factory)
    {
        _factory = factory;
        _factory.SeedDatabase();
    }

    private async Task<(Guid MilestoneId, ApiContractClient ExpertClient)> CreateProjectWithMilestoneAsync(ApiContractClient client, string label, decimal amount)
    {
        var createJobReq = new
        {
            title = $"Step Board {label}",
            originalDescription = "Job for checking milestone steps",
            categoryId = ApiContractTestData.CategoryId,
            budgetType = "FIXED",
            budgetMin = amount,
            budgetMax = amount,
            currency = "AICOIN",
            timelineDays = 14,
            visibility = "PUBLIC",
            skillIds = new List<Guid> { ApiContractTestData.SkillId }
        };
        var (_, jobBody) = await client.PostAsync("/api/v1/jobs", createJobReq);
        var jobIdStr = jobBody?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(jobIdStr, out var jobId);
        await client.PostAsync($"/api/v1/jobs/{jobId}/publish", new { });

        var expertClient = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.EXPERT));
        var createProposalReq = new
        {
            jobId,
            coverLetter = $"Proposal for {label}.",
            proposedBudget = amount,
            proposedTimelineDays = 10,
            milestones = new List<object>
            {
                new { title = $"{label} Milestone", description = "Deliverable", amount, dueDays = 5, orderIndex = 1, acceptanceCriteria = "Criteria" }
            }
        };
        var (_, propBody) = await expertClient.PostAsync($"/api/v1/jobs/{jobId}/proposals", createProposalReq);
        var proposalIdStr = propBody?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(proposalIdStr, out var proposalId);

        var (_, accBody) = await client.PutEmptyAsync($"/api/v1/proposals/{proposalId}/accept");
        var projectIdStr = accBody?.GetProperty("data").GetProperty("projectId").GetString();
        Guid.TryParse(projectIdStr, out var projectId);

        var (_, getProjBody) = await client.GetAsync($"/api/v1/projects/{projectId}");
        var milestonesArray = getProjBody?.GetProperty("data").GetProperty("milestones");
        var milestoneIdStr = milestonesArray?[0].GetProperty("id").GetString();
        Guid.TryParse(milestoneIdStr, out var milestoneId);

        return (milestoneId, expertClient);
    }

    [Fact]
    public async Task Run_Flow3_MilestoneStep_API_Verification_Sequence()
    {
        var client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.CLIENT));
        var (milestoneId, expertClient) = await CreateProjectWithMilestoneAsync(client, "Steps", 900);

        // Get initial steps (should contain the default system step 'Created')
        var (_, initialStepsBody) = await client.GetAsync($"/api/v1/milestones/{milestoneId}/steps");
        var initialStepsArray = initialStepsBody?.GetProperty("data");
        var createdStepIdStr = initialStepsArray?[0].GetProperty("id").GetString();
        Guid.TryParse(createdStepIdStr, out var createdStepId);

        // 1. POST /api/v1/milestones/{id}/steps — Expert can create
        var (createStep1Res, createStep1Body) = await expertClient.PostAsync(
            $"/api/v1/milestones/{milestoneId}/steps",
            new { title = "Step 1", description = "First step", orderIndex = 1 });
        createStep1Res.StatusCode.Should().Be(HttpStatusCode.OK);
        bool createStep1Success = createStep1Body?.GetProperty("success").GetBoolean() ?? false;
        var step1IdStr = createStep1Body?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(step1IdStr, out var step1Id);
        _factory.Tracker.Record(
            "Flow 3", "POST", "/api/v1/milestones/{id}/steps", 200, (int)createStep1Res.StatusCode,
            requestMatchesDoc: true, responseMatchesDoc: createStep1Success
        );

        var (createStep2Res, createStep2Body) = await expertClient.PostAsync(
            $"/api/v1/milestones/{milestoneId}/steps",
            new { title = "Step 2", description = "Second step", orderIndex = 2 });
        createStep2Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var step2IdStr = createStep2Body?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(step2IdStr, out var step2Id);

        var (createStep3Res, createStep3Body) = await expertClient.PostAsync(
            $"/api/v1/milestones/{milestoneId}/steps",
            new { title = "Step 3", description = "Third step", orderIndex = 3 });
        createStep3Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var step3IdStr = createStep3Body?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(step3IdStr, out var step3Id);

        // 2. Client cannot create steps
        var (clientCreateRes, _) = await client.PostAsync(
            $"/api/v1/milestones/{milestoneId}/steps",
            new { title = "Client Step", orderIndex = 4 });
        clientCreateRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 3. GET /api/v1/milestones/{id}/steps — either party
        var (getStepsRes, getStepsBody) = await client.GetAsync($"/api/v1/milestones/{milestoneId}/steps");
        getStepsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool getStepsSuccess = getStepsBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Flow 3", "GET", "/api/v1/milestones/{id}/steps", 200, (int)getStepsRes.StatusCode,
            requestMatchesDoc: true, responseMatchesDoc: getStepsSuccess
        );

        // 4. PUT /api/v1/steps/{id} — Expert can update, Client cannot
        var (updateStepRes, updateStepBody) = await expertClient.PutAsync(
            $"/api/v1/steps/{step1Id}", new { title = "Step 1 Updated" });
        updateStepRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool updateStepSuccess = updateStepBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Flow 3", "PUT", "/api/v1/steps/{id}", 200, (int)updateStepRes.StatusCode,
            requestMatchesDoc: true, responseMatchesDoc: updateStepSuccess
        );

        var (clientUpdateRes, _) = await client.PutAsync($"/api/v1/steps/{step1Id}", new { title = "Nope" });
        clientUpdateRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // System step cannot be modified
        var (systemUpdateRes, _) = await expertClient.PutAsync($"/api/v1/steps/{createdStepId}", new { title = "Hacked" });
        systemUpdateRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 5. PUT /api/v1/milestones/{id}/steps/reorder — Expert only
        var (reorderRes, reorderBody) = await expertClient.PutAsync(
            $"/api/v1/milestones/{milestoneId}/steps/reorder",
            new List<Guid> { createdStepId, step3Id, step2Id, step1Id });
        reorderRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool reorderSuccess = reorderBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Flow 3", "PUT", "/api/v1/milestones/{id}/steps/reorder", 200, (int)reorderRes.StatusCode,
            requestMatchesDoc: true, responseMatchesDoc: reorderSuccess
        );

        var (clientReorderRes, _) = await client.PutAsync(
            $"/api/v1/milestones/{milestoneId}/steps/reorder", new List<Guid> { createdStepId, step1Id, step2Id, step3Id });
        clientReorderRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // 6. PUT /api/v1/steps/{id}/status — full transition matrix by Expert
        var (startRes, startBody) = await expertClient.PutAsync(
            $"/api/v1/steps/{step1Id}/status", new { status = "IN_PROGRESS" });
        startRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool startSuccess = startBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Flow 3", "PUT", "/api/v1/steps/{id}/status", 200, (int)startRes.StatusCode,
            requestMatchesDoc: true, responseMatchesDoc: startSuccess
        );

        // Client cannot update status (domain-level rejection, not a policy-level one -> 401)
        var (clientSkipRes, _) = await client.PutAsync($"/api/v1/steps/{step2Id}/status", new { status = "SKIPPED" });
        clientSkipRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Expert can skip
        var (skipRes, _) = await expertClient.PutAsync($"/api/v1/steps/{step2Id}/status", new { status = "SKIPPED" });
        skipRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // SKIPPED is terminal — no further transition
        var (reopenSkippedRes, _) = await expertClient.PutAsync($"/api/v1/steps/{step2Id}/status", new { status = "IN_PROGRESS" });
        reopenSkippedRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // PENDING/IN_PROGRESS -> COMPLETED
        var (completeRes, _) = await expertClient.PutAsync($"/api/v1/steps/{step1Id}/status", new { status = "COMPLETED" });
        completeRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // COMPLETED is terminal — no further transition
        var (reopenCompletedRes, _) = await expertClient.PutAsync($"/api/v1/steps/{step1Id}/status", new { status = "IN_PROGRESS" });
        reopenCompletedRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Direct transition back to PENDING is disallowed (from a non-terminal state)
        await expertClient.PutAsync($"/api/v1/steps/{step3Id}/status", new { status = "IN_PROGRESS" });
        var (backToPendingRes, _) = await expertClient.PutAsync($"/api/v1/steps/{step3Id}/status", new { status = "PENDING" });
        backToPendingRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 7. DELETE /api/v1/steps/{id} — Expert only
        var (clientDeleteRes, _) = await client.DeleteAsync($"/api/v1/steps/{step3Id}");
        clientDeleteRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // System step cannot be deleted
        var (systemDeleteRes, _) = await expertClient.DeleteAsync($"/api/v1/steps/{createdStepId}");
        systemDeleteRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var (deleteRes, deleteBody) = await expertClient.DeleteAsync($"/api/v1/steps/{step3Id}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool deleteSuccess = deleteBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Flow 3", "DELETE", "/api/v1/steps/{id}", 200, (int)deleteRes.StatusCode,
            requestMatchesDoc: true, responseMatchesDoc: deleteSuccess
        );

        // 8. Step mutation rejected once the Milestone is finalized
        var (finalizedMilestoneId, finalizedExpertClient) = await CreateProjectWithMilestoneAsync(client, "Finalized", 400);
        await client.PutEmptyAsync($"/api/v1/milestones/{finalizedMilestoneId}/fund");
        await finalizedExpertClient.PostAsync($"/api/v1/milestones/{finalizedMilestoneId}/deliverables", new
        {
            description = "Done",
            fileUrl = "https://example.com/deliverable.zip",
            demoUrl = "https://example.com/demo"
        });
        await client.PutEmptyAsync($"/api/v1/milestones/{finalizedMilestoneId}/approve");

        var (finalizedAddRes, _) = await finalizedExpertClient.PostAsync(
            $"/api/v1/milestones/{finalizedMilestoneId}/steps", new { title = "Too late", orderIndex = 1 });
        finalizedAddRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        _factory.Tracker.ExportResults();
    }

    [Fact]
    public async Task Run_Flow3_MilestoneStep_BlockUnblock_Sequence()
    {
        var client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.CLIENT));
        var (milestoneId, expertClient) = await CreateProjectWithMilestoneAsync(client, "BlockUnblock", 700);

        var (createStepRes, createStepBody) = await expertClient.PostAsync(
            $"/api/v1/milestones/{milestoneId}/steps",
            new { title = "Blockable step", description = "Needs client input", orderIndex = 1 });
        createStepRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var stepIdStr = createStepBody?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(stepIdStr, out var stepId);

        // Cannot block a step that isn't IN_PROGRESS yet
        var (blockPendingRes, _) = await expertClient.PutAsync(
            $"/api/v1/steps/{stepId}/status", new { status = "BLOCKED", reason = "too early" });
        blockPendingRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await expertClient.PutAsync($"/api/v1/steps/{stepId}/status", new { status = "IN_PROGRESS" });

        // Blocking without a reason is rejected
        var (blockNoReasonRes, _) = await expertClient.PutAsync(
            $"/api/v1/steps/{stepId}/status", new { status = "BLOCKED" });
        blockNoReasonRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Client cannot block (domain-level rejection -> 401)
        var (clientBlockRes, _) = await client.PutAsync(
            $"/api/v1/steps/{stepId}/status", new { status = "BLOCKED", reason = "client trying to block" });
        clientBlockRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Expert blocks with a reason
        var (blockRes, blockBody) = await expertClient.PutAsync(
            $"/api/v1/steps/{stepId}/status", new { status = "BLOCKED", reason = "Waiting on client access" });
        blockRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool blockSuccess = blockBody?.GetProperty("success").GetBoolean() ?? false;
        blockBody?.GetProperty("data").GetProperty("blockedReason").GetString().Should().Be("Waiting on client access");
        _factory.Tracker.Record(
            "Flow 3", "PUT", "/api/v1/steps/{id}/status (block)", 200, (int)blockRes.StatusCode,
            requestMatchesDoc: true, responseMatchesDoc: blockSuccess
        );

        // Expert cannot unblock (domain-level rejection -> 401)
        var (expertUnblockRes, _) = await expertClient.PutAsync(
            $"/api/v1/steps/{stepId}/status", new { status = "IN_PROGRESS" });
        expertUnblockRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Client unblocks
        var (unblockRes, unblockBody) = await client.PutAsync(
            $"/api/v1/steps/{stepId}/status", new { status = "IN_PROGRESS" });
        unblockRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool unblockSuccess = unblockBody?.GetProperty("success").GetBoolean() ?? false;
        unblockBody?.GetProperty("data").GetProperty("status").GetString().Should().Be("IN_PROGRESS");
        _factory.Tracker.Record(
            "Flow 3", "PUT", "/api/v1/steps/{id}/status (unblock)", 200, (int)unblockRes.StatusCode,
            requestMatchesDoc: true, responseMatchesDoc: unblockSuccess
        );

        _factory.Tracker.ExportResults();
    }

    [Fact]
    public async Task Run_Flow3_MilestoneStep_SuggestSteps_Sequence()
    {
        var client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.CLIENT));
        var (milestoneId, expertClient) = await CreateProjectWithMilestoneAsync(client, "Suggest", 800);

        // Expert can request suggestions; nothing is persisted by the call alone
        var (suggestRes, suggestBody) = await expertClient.PostAsync($"/api/v1/milestones/{milestoneId}/steps/suggest", new { });
        suggestRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool suggestSuccess = suggestBody?.GetProperty("success").GetBoolean() ?? false;
        var steps = suggestBody?.GetProperty("data").GetProperty("steps");
        steps.HasValue.Should().BeTrue();
        steps!.Value.GetArrayLength().Should().BeGreaterThan(0);
        _factory.Tracker.Record(
            "Flow 3", "POST", "/api/v1/milestones/{id}/steps/suggest", 200, (int)suggestRes.StatusCode,
            requestMatchesDoc: true, responseMatchesDoc: suggestSuccess
        );

        var (getStepsAfterSuggestRes, getStepsAfterSuggestBody) = await client.GetAsync($"/api/v1/milestones/{milestoneId}/steps");
        getStepsAfterSuggestRes.StatusCode.Should().Be(HttpStatusCode.OK);
        getStepsAfterSuggestBody?.GetProperty("data").GetArrayLength().Should().Be(1); // 1 system step 'Created'

        // Non-existent milestone returns 404 NotFound
        var nonExistentId = Guid.NewGuid();
        var (notFoundSuggestRes, _) = await expertClient.PostAsync($"/api/v1/milestones/{nonExistentId}/steps/suggest", new { });
        notFoundSuggestRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Client cannot request suggestions
        var (clientSuggestRes, _) = await client.PostAsync($"/api/v1/milestones/{milestoneId}/steps/suggest", new { });
        clientSuggestRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _factory.Tracker.ExportResults();
    }
}
