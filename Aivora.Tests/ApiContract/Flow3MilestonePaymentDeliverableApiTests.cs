using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Aivora.Tests.ApiContract;

public class Flow3MilestonePaymentDeliverableApiTests : IClassFixture<ApiContractTestFactory>
{
    private readonly ApiContractTestFactory _factory;

    public Flow3MilestonePaymentDeliverableApiTests(ApiContractTestFactory factory)
    {
        _factory = factory;
        _factory.SeedDatabase();
    }

    [Fact]
    public async Task Run_Flow3_API_Verification_Sequence()
    {
        var client = new ApiContractClient(_factory.CreateClient());

        // 1. Setup: Client creates job, Expert submits proposal, Client accepts proposal (creates project)
        await client.LoginAsClientAsync();
        var createJobReq = new
        {
            title = "Flow 3 Job",
            originalDescription = "Job for checking milestones and deliverables",
            categoryId = ApiContractTestData.CategoryId,
            budgetType = "FIXED",
            budgetMin = 1000,
            budgetMax = 2000,
            currency = "AICOIN",
            timelineDays = 14,
            visibility = "PUBLIC",
            skillIds = new List<Guid> { ApiContractTestData.SkillId }
        };
        var (_, jobBody) = await client.PostAsync("/api/v1/jobs", createJobReq);
        var jobIdStr = jobBody?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(jobIdStr, out var jobId);

        await client.PostAsync($"/api/v1/jobs/{jobId}/publish", new { });

        await client.LoginAsExpertAsync();
        var createProposalReq = new
        {
            jobId = jobId,
            coverLetter = "Proposal for flow 3.",
            proposedBudget = 1200,
            proposedTimelineDays = 10,
            milestones = new List<object>
            {
                new { title = "Flow 3 M1", description = "Deliverable 1", amount = 1200, dueDays = 5, orderIndex = 1, acceptanceCriteria = "Criteria 1" }
            }
        };
        var (_, propBody) = await client.PostAsync($"/api/v1/jobs/{jobId}/proposals", createProposalReq);
        var proposalIdStr = propBody?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(proposalIdStr, out var proposalId);

        await client.LoginAsClientAsync();
        var (_, accBody) = await client.PutEmptyAsync($"/api/v1/proposals/{proposalId}/accept");
        var projectIdStr = accBody?.GetProperty("data").GetProperty("projectId").GetString();
        Guid.TryParse(projectIdStr, out var projectId);

        // 2. GET /api/v1/projects
        var (listProjectsRes, listProjectsBody) = await client.GetAsync("/api/v1/projects?page=1&pageSize=10");
        listProjectsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool listProjectsSuccess = listProjectsBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 3",
            "GET",
            "/api/v1/projects",
            200,
            (int)listProjectsRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: listProjectsSuccess
        );

        // 3. GET /api/v1/projects/{id}
        var (getProjRes, getProjBody) = await client.GetAsync($"/api/v1/projects/{projectId}");
        getProjRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool getProjSuccess = getProjBody?.GetProperty("success").GetBoolean() ?? false;
        var milestonesArray = getProjBody?.GetProperty("data").GetProperty("milestones");
        var milestoneIdStr = milestonesArray?[0].GetProperty("id").GetString();
        Guid.TryParse(milestoneIdStr, out var milestoneId);

        ApiVerificationTracker.Record(
            "Flow 3",
            "GET",
            "/api/v1/projects/{id}",
            200,
            (int)getProjRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: getProjSuccess
        );

        // 4. POST /api/v1/projects/{id}/milestones (Create custom milestone)
        var createMreq = new
        {
            title = "Custom Milestone",
            description = "Extra phase",
            amount = 500,
            dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(20)),
            orderIndex = 2
        };
        var (createMres, createMbody) = await client.PostAsync($"/api/v1/projects/{projectId}/milestones", createMreq);
        createMres.StatusCode.Should().Be(HttpStatusCode.OK);
        bool createMsuccess = createMbody?.GetProperty("success").GetBoolean() ?? false;
        var customMilestoneIdStr = createMbody?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(customMilestoneIdStr, out var customMilestoneId);

        ApiVerificationTracker.Record(
            "Flow 3",
            "POST",
            "/api/v1/projects/{id}/milestones",
            200,
            (int)createMres.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: createMsuccess
        );

        // 5. GET /api/v1/milestones/{id}
        var (getMres, getMbody) = await client.GetAsync($"/api/v1/milestones/{milestoneId}");
        getMres.StatusCode.Should().Be(HttpStatusCode.OK);
        bool getMsuccess = getMbody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 3",
            "GET",
            "/api/v1/milestones/{id}",
            200,
            (int)getMres.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: getMsuccess
        );

        // 6. PUT /api/v1/milestones/{id}
        var updateMreq = new
        {
            title = "Updated custom milestone title",
            amount = 600
        };
        var (updateMres, updateMbody) = await client.PutAsync($"/api/v1/milestones/{customMilestoneId}", updateMreq);
        updateMres.StatusCode.Should().Be(HttpStatusCode.OK);
        bool updateMsuccess = updateMbody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 3",
            "PUT",
            "/api/v1/milestones/{id}",
            200,
            (int)updateMres.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: updateMsuccess
        );

        // 7. POST /api/v1/wallet/deposit-demo
        var depositReq = new { amount = 2000, description = "Test deposit" };
        var (depRes, depBody) = await client.PostAsync("/api/v1/wallet/deposit-demo", depositReq);
        depRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool depSuccess = depBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 3",
            "POST",
            "/api/v1/wallet/deposit-demo",
            200,
            (int)depRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: depSuccess
        );

        // 8. GET /api/v1/wallet/me
        var (wRes, wBody) = await client.GetAsync("/api/v1/wallet/me");
        wRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool wSuccess = wBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 3",
            "GET",
            "/api/v1/wallet/me",
            200,
            (int)wRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: wSuccess
        );

        // 9. PUT /api/v1/milestones/{id}/fund
        var (fundRes, fundBody) = await client.PutEmptyAsync($"/api/v1/milestones/{milestoneId}/fund");
        fundRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool fundSuccess = fundBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 3",
            "PUT",
            "/api/v1/milestones/{id}/fund",
            200,
            (int)fundRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: fundSuccess
        );

        // 10. GET /api/v1/payments/history
        var (payHistoryRes, payHistoryBody) = await client.GetAsync("/api/v1/payments/history?page=1&pageSize=10");
        payHistoryRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool payHistorySuccess = payHistoryBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 3",
            "GET",
            "/api/v1/payments/history",
            200,
            (int)payHistoryRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: payHistorySuccess
        );

        // 11. POST /api/v1/milestones/{id}/deliverables (Expert submits deliverable)
        await client.LoginAsExpertAsync();
        var submitDelReq = new
        {
            description = "Completed chatbot implementation",
            fileUrl = "https://example.com/deliverable.zip",
            demoUrl = "https://example.com/demo"
        };
        var (delRes, delBody) = await client.PostAsync($"/api/v1/milestones/{milestoneId}/deliverables", submitDelReq);
        delRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool delSuccess = delBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 3",
            "POST",
            "/api/v1/milestones/{id}/deliverables",
            200,
            (int)delRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: delSuccess
        );

        // 12. GET /api/v1/milestones/{id}/deliverables
        await client.LoginAsClientAsync();
        var (getDelsRes, getDelsBody) = await client.GetAsync($"/api/v1/milestones/{milestoneId}/deliverables");
        getDelsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool getDelsSuccess = getDelsBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 3",
            "GET",
            "/api/v1/milestones/{id}/deliverables",
            200,
            (int)getDelsRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: getDelsSuccess
        );

        // 13. PUT /api/v1/milestones/{id}/request-revision
        var (revRes, revBody) = await client.PutAsync($"/api/v1/milestones/{milestoneId}/request-revision", "Need dark mode layout");
        revRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool revSuccess = revBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 3",
            "PUT",
            "/api/v1/milestones/{id}/request-revision",
            200,
            (int)revRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: revSuccess
        );

        // 14. POST /api/v1/milestones/{id}/dispute
        // Let's open a dispute on this milestone. First expert resubmits, or we can just call it
        // The API allows opening a dispute on a milestone when it's funded/in-revision
        var (dispRes, dispBody) = await client.PostAsync($"/api/v1/milestones/{milestoneId}/dispute", "Dispute milestone revision requests");
        dispRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool dispSuccess = dispBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 3",
            "POST",
            "/api/v1/milestones/{id}/dispute",
            200,
            (int)dispRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: dispSuccess
        );

        // 15. Setup a fresh project/milestone to approve (since milestoneId is now disputed)
        var createJobReq2 = new
        {
            title = "Flow 3 Job 2",
            originalDescription = "Job for checking approval",
            categoryId = ApiContractTestData.CategoryId,
            budgetType = "FIXED",
            budgetMin = 500,
            budgetMax = 500,
            currency = "AICOIN",
            timelineDays = 14,
            visibility = "PUBLIC",
            skillIds = new List<Guid> { ApiContractTestData.SkillId }
        };
        var (_, jobBody2) = await client.PostAsync("/api/v1/jobs", createJobReq2);
        var jobIdStr2 = jobBody2?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(jobIdStr2, out var jobId2);

        await client.PostAsync($"/api/v1/jobs/{jobId2}/publish", new { });

        await client.LoginAsExpertAsync();
        var createProposalReq2 = new
        {
            jobId = jobId2,
            coverLetter = "Proposal for flow 3 approval.",
            proposedBudget = 500,
            proposedTimelineDays = 10,
            milestones = new List<object>
            {
                new { title = "Flow 3 M2", description = "Deliverable 2", amount = 500, dueDays = 5, orderIndex = 1, acceptanceCriteria = "Criteria 2" }
            }
        };
        var (_, propBody2) = await client.PostAsync($"/api/v1/jobs/{jobId2}/proposals", createProposalReq2);
        var proposalIdStr2 = propBody2?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(proposalIdStr2, out var proposalId2);

        await client.LoginAsClientAsync();
        var (_, accBody2) = await client.PutEmptyAsync($"/api/v1/proposals/{proposalId2}/accept");
        var projectIdStr2 = accBody2?.GetProperty("data").GetProperty("projectId").GetString();
        Guid.TryParse(projectIdStr2, out var projectId2);

        var (getProjRes2, getProjBody2) = await client.GetAsync($"/api/v1/projects/{projectId2}");
        var milestonesArray2 = getProjBody2?.GetProperty("data").GetProperty("milestones");
        var milestoneIdStr2 = milestonesArray2?[0].GetProperty("id").GetString();
        Guid.TryParse(milestoneIdStr2, out var milestoneId2);

        // Fund milestone 2
        await client.PutEmptyAsync($"/api/v1/milestones/{milestoneId2}/fund");

        // Expert submits deliverable for milestone 2
        await client.LoginAsExpertAsync();
        await client.PostAsync($"/api/v1/milestones/{milestoneId2}/deliverables", submitDelReq);

        // Client approves milestone 2
        await client.LoginAsClientAsync();
        var (appRes, appBody) = await client.PutEmptyAsync($"/api/v1/milestones/{milestoneId2}/approve");
        appRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool appSuccess = appBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 3",
            "PUT",
            "/api/v1/milestones/{id}/approve",
            200,
            (int)appRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: appSuccess
        );

        // 16. PUT /api/v1/projects/{id}/cancel
        // We need to cancel a newly accepted, unfunded project.
        var createJobReq3 = new
        {
            title = "Flow 3 Job 3",
            originalDescription = "Job for checking project cancellation",
            categoryId = ApiContractTestData.CategoryId,
            budgetType = "FIXED",
            budgetMin = 300,
            budgetMax = 300,
            currency = "AICOIN",
            timelineDays = 14,
            visibility = "PUBLIC",
            skillIds = new List<Guid> { ApiContractTestData.SkillId }
        };
        var (_, jobBody3) = await client.PostAsync("/api/v1/jobs", createJobReq3);
        var jobIdStr3 = jobBody3?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(jobIdStr3, out var jobId3);

        await client.PostAsync($"/api/v1/jobs/{jobId3}/publish", new { });

        await client.LoginAsExpertAsync();
        var createProposalReq3 = new
        {
            jobId = jobId3,
            coverLetter = "Proposal for flow 3 cancellation test.",
            proposedBudget = 300,
            proposedTimelineDays = 10,
            milestones = new List<object>
            {
                new { title = "Flow 3 M3", description = "Deliverable 3", amount = 300, dueDays = 5, orderIndex = 1, acceptanceCriteria = "Criteria 3" }
            }
        };
        var (_, propBody3) = await client.PostAsync($"/api/v1/jobs/{jobId3}/proposals", createProposalReq3);
        var proposalIdStr3 = propBody3?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(proposalIdStr3, out var proposalId3);

        await client.LoginAsClientAsync();
        var (_, accBody3) = await client.PutEmptyAsync($"/api/v1/proposals/{proposalId3}/accept");
        var projectIdStr3 = accBody3?.GetProperty("data").GetProperty("projectId").GetString();
        Guid.TryParse(projectIdStr3, out var projectId3);

        var (cancelProjRes, cancelProjBody) = await client.PutAsync($"/api/v1/projects/{projectId3}/cancel", "Project cancelled by client");
        cancelProjRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool cancelProjSuccess = cancelProjBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 3",
            "PUT",
            "/api/v1/projects/{id}/cancel",
            200,
            (int)cancelProjRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: cancelProjSuccess
        );

        // Export all results at the end of the test run
        ApiVerificationTracker.ExportResults();
    }
}
