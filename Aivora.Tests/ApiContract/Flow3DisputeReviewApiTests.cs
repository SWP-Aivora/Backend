using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Aivora.Repositories.Enums;
using FluentAssertions;
using Xunit;

namespace Aivora.Tests.ApiContract;

public class Flow3DisputeReviewApiTests : IClassFixture<ApiContractTestFactory>
{
    private readonly ApiContractTestFactory _factory;

    public Flow3DisputeReviewApiTests(ApiContractTestFactory factory)
    {
        _factory = factory;
        _factory.SeedDatabase();
    }

    [Fact]
    public async Task Run_Flow3_DisputeReview_API_Verification_Sequence()
    {
        var client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.CLIENT));

        // 1. Setup: Create job, apply proposal, accept proposal, fund milestone
        client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.CLIENT));
        var createJobReq = new
        {
            title = "Flow 3 Dispute Review Job",
            originalDescription = "Job for checking disputes and reviews",
            categoryId = ApiContractTestData.CategoryId,
            budgetType = "FIXED",
            budgetMin = 1000,
            budgetMax = 1000,
            currency = "AICOIN",
            timelineDays = 14,
            visibility = "PUBLIC",
            skillIds = new List<Guid> { ApiContractTestData.SkillId }
        };
        var (_, jobBody) = await client.PostAsync("/api/v1/jobs", createJobReq);
        var jobIdStr = jobBody?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(jobIdStr, out var jobId);

        await client.PostAsync($"/api/v1/jobs/{jobId}/publish", new { });

        client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.EXPERT));
        var createProposalReq = new
        {
            jobId = jobId,
            coverLetter = "Proposal for flow 3 dispute review.",
            proposedBudget = 1000,
            proposedTimelineDays = 10,
            milestones = new List<object>
            {
                new { title = "Flow 3 Dispute Review M1", description = "Deliverable 1", amount = 1000, dueDays = 5, orderIndex = 1, acceptanceCriteria = "Criteria 1" }
            }
        };
        var (_, propBody) = await client.PostAsync($"/api/v1/jobs/{jobId}/proposals", createProposalReq);
        var proposalIdStr = propBody?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(proposalIdStr, out var proposalId);

        client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.CLIENT));
        var (_, accBody) = await client.PutEmptyAsync($"/api/v1/proposals/{proposalId}/accept");
        var projectIdStr = accBody?.GetProperty("data").GetProperty("projectId").GetString();
        Guid.TryParse(projectIdStr, out var projectId);

        var (_, getProjBody) = await client.GetAsync($"/api/v1/projects/{projectId}");
        var milestoneIdStr = getProjBody?.GetProperty("data").GetProperty("milestones")[0].GetProperty("id").GetString();
        Guid.TryParse(milestoneIdStr, out var milestoneId);

        // Fund milestone
        await client.PutEmptyAsync($"/api/v1/milestones/{milestoneId}/fund");

        // 2. POST /api/v1/disputes
        var openDisReq = new
        {
            milestoneId = milestoneId,
            reason = "Quality of work is poor",
            description = "Detailed explanation of quality issues."
        };
        var (openDisRes, openDisBody) = await client.PostAsync("/api/v1/disputes", openDisReq);
        openDisRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool openDisSuccess = openDisBody?.GetProperty("success").GetBoolean() ?? false;
        var disputeIdStr = openDisBody?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(disputeIdStr, out var disputeId);

        _factory.Tracker.Record(
            "Flow 3",
            "POST",
            "/api/v1/disputes",
            200,
            (int)openDisRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: openDisSuccess && disputeId != Guid.Empty
        );

        // 3. GET /api/v1/disputes
        var (listDisRes, listDisBody) = await client.GetAsync("/api/v1/disputes?page=1&pageSize=10");
        listDisRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool listDisSuccess = listDisBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Flow 3",
            "GET",
            "/api/v1/disputes",
            200,
            (int)listDisRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: listDisSuccess
        );

        // 4. GET /api/v1/disputes/{id}
        var (getDisRes, getDisBody) = await client.GetAsync($"/api/v1/disputes/{disputeId}");
        getDisRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool getDisSuccess = getDisBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Flow 3",
            "GET",
            "/api/v1/disputes/{id}",
            200,
            (int)getDisRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: getDisSuccess
        );

        // 5. POST /api/v1/disputes/{id}/evidence
        var addEvReq = new
        {
            content = "Screenshots showing failures in mobile layouts",
            fileUrl = "https://example.com/evidence.png"
        };
        var (addEvRes, addEvBody) = await client.PostAsync($"/api/v1/disputes/{disputeId}/evidence", addEvReq);
        addEvRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool addEvSuccess = addEvBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Flow 3",
            "POST",
            "/api/v1/disputes/{id}/evidence",
            200,
            (int)addEvRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: addEvSuccess
        );

        // 6. PUT /api/v1/disputes/{id}/resolve (Admin only)
        client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.ADMIN));
        var resolveReq = new
        {
            resolutionType = "REFUND_TO_CLIENT",
            resolutionNote = "Work quality did not meet requirements; refunding client.",
            releaseAmount = 0,
            refundAmount = 1000
        };
        var (resolveRes, resolveBody) = await client.PutAsync($"/api/v1/disputes/{disputeId}/resolve", resolveReq);
        resolveRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool resolveSuccess = resolveBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Flow 3",
            "PUT",
            "/api/v1/disputes/{id}/resolve",
            200,
            (int)resolveRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: resolveSuccess
        );

        // 7. POST /api/v1/reviews
        client = new ApiContractClient(_factory.CreateAuthenticatedClient(UserRole.CLIENT));
        var createReviewReq = new
        {
            projectId = projectId,
            revieweeId = ApiContractTestData.ExpertUserId,
            rating = 4,
            comment = "Good developer, though communication was a bit slow.",
            communicationRating = 3,
            qualityRating = 5,
            deadlineRating = 4
        };
        var (reviewRes, reviewBody) = await client.PostAsync("/api/v1/reviews", createReviewReq);
        reviewRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool reviewSuccess = reviewBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Flow 3",
            "POST",
            "/api/v1/reviews",
            200,
            (int)reviewRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: reviewSuccess
        );

        // 8. GET /api/v1/users/{id}/reviews
        var (getReviewsRes, getReviewsBody) = await client.GetAsync($"/api/v1/users/{ApiContractTestData.ExpertUserId}/reviews?page=1&pageSize=10");
        getReviewsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool getReviewsSuccess = getReviewsBody?.GetProperty("success").GetBoolean() ?? false;
        _factory.Tracker.Record(
            "Flow 3",
            "GET",
            "/api/v1/users/{id}/reviews",
            200,
            (int)getReviewsRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: getReviewsSuccess
        );

        // Export all results at the end of the test run
        _factory.Tracker.ExportResults();
    }
}
