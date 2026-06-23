using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Aivora.Tests.ApiContract;

public class Flow2ProposalProjectApiTests : IClassFixture<ApiContractTestFactory>
{
    private readonly ApiContractTestFactory _factory;

    public Flow2ProposalProjectApiTests(ApiContractTestFactory factory)
    {
        _factory = factory;
        _factory.SeedDatabase();
    }

    [Fact]
    public async Task Run_Flow2_API_Verification_Sequence()
    {
        var client = new ApiContractClient(_factory.CreateClient());

        // 1. Setup: Client creates and publishes a JobPost
        await client.LoginAsClientAsync();
        var createJobReq = new
        {
            title = "Flow 2 Job",
            originalDescription = "Job for checking proposals and projects",
            categoryId = ApiContractTestData.CategoryId,
            budgetType = "FIXED",
            budgetMin = 1000,
            budgetMax = 2000,
            currency = "AICOIN",
            timelineDays = 14,
            visibility = "PUBLIC",
            skillIds = new List<Guid> { ApiContractTestData.SkillId }
        };
        var (jobRes, jobBody) = await client.PostAsync("/api/v1/jobs", createJobReq);
        var jobIdStr = jobBody?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(jobIdStr, out var jobId);

        await client.PostAsync($"/api/v1/jobs/{jobId}/publish", new { });

        // 2. Expert logins and submits proposal
        await client.LoginAsExpertAsync();
        var createProposalReq = new
        {
            jobId = jobId,
            coverLetter = "I am a skilled React engineer.",
            proposedBudget = 1500,
            proposedTimelineDays = 10,
            milestones = new List<object>
            {
                new { title = "Milestone 1", description = "Deliver mockups", amount = 500, dueDays = 3, orderIndex = 1, acceptanceCriteria = "Accept criteria 1" },
                new { title = "Milestone 2", description = "Deliver backend", amount = 1000, dueDays = 7, orderIndex = 2, acceptanceCriteria = "Accept criteria 2" }
            }
        };

        var (propRes, propBody) = await client.PostAsync($"/api/v1/jobs/{jobId}/proposals", createProposalReq);
        propRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool propSuccess = propBody?.GetProperty("success").GetBoolean() ?? false;
        var proposalIdStr = propBody?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(proposalIdStr, out var proposalId);

        ApiVerificationTracker.Record(
            "Flow 2",
            "POST",
            "/api/v1/jobs/{id}/proposals",
            200,
            (int)propRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: propSuccess && proposalId != Guid.Empty
        );

        // 3. Client logins and views proposals for the job
        await client.LoginAsClientAsync();
        var (getPropsRes, getPropsBody) = await client.GetAsync($"/api/v1/jobs/{jobId}/proposals");
        getPropsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool getPropsSuccess = getPropsBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 2",
            "GET",
            "/api/v1/jobs/{id}/proposals",
            200,
            (int)getPropsRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: getPropsSuccess
        );

        // 4. Client views proposal details
        var (getPropRes, getPropBody) = await client.GetAsync($"/api/v1/proposals/{proposalId}");
        getPropRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool getPropSuccess = getPropBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 2",
            "GET",
            "/api/v1/proposals/{id}",
            200,
            (int)getPropRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: getPropSuccess
        );

        // 5. Expert views their own proposals
        await client.LoginAsExpertAsync();
        var (getMyPropsRes, getMyPropsBody) = await client.GetAsync("/api/v1/proposals/me");
        getMyPropsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool getMyPropsSuccess = getMyPropsBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 2",
            "GET",
            "/api/v1/proposals/me",
            200,
            (int)getMyPropsRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: getMyPropsSuccess
        );

        // 6. Client shortlists proposal
        await client.LoginAsClientAsync();
        var (slRes, slBody) = await client.PutEmptyAsync($"/api/v1/proposals/{proposalId}/shortlist");
        slRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool slSuccess = slBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 2",
            "PUT",
            "/api/v1/proposals/{id}/shortlist",
            200,
            (int)slRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: slSuccess
        );

        // 7. Expert withdraws proposal (Create new job and proposal first)
        await client.LoginAsClientAsync();
        var createJobReq2 = new
        {
            title = "Flow 2 Job 2",
            originalDescription = "Job for checking proposal withdraw",
            categoryId = ApiContractTestData.CategoryId,
            budgetType = "FIXED",
            budgetMin = 1000,
            budgetMax = 2000,
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
            coverLetter = "Proposal for withdraw test.",
            proposedBudget = 1000,
            proposedTimelineDays = 5,
            milestones = new List<object>
            {
                new { title = "M1", description = "D1", amount = 1000, dueDays = 5, orderIndex = 1, acceptanceCriteria = "A1" }
            }
        };
        var (_, propBody2) = await client.PostAsync($"/api/v1/jobs/{jobId2}/proposals", createProposalReq2);
        var proposalIdStr2 = propBody2?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(proposalIdStr2, out var proposalId2);

        var (wRes, wBody) = await client.PutEmptyAsync($"/api/v1/proposals/{proposalId2}/withdraw");
        wRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool wSuccess = wBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 2",
            "PUT",
            "/api/v1/proposals/{id}/withdraw",
            200,
            (int)wRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: wSuccess
        );

        // 8. Client rejects proposal (Create new job and proposal first)
        await client.LoginAsClientAsync();
        var createJobReq3 = new
        {
            title = "Flow 2 Job 3",
            originalDescription = "Job for checking proposal reject",
            categoryId = ApiContractTestData.CategoryId,
            budgetType = "FIXED",
            budgetMin = 1000,
            budgetMax = 2000,
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
            coverLetter = "Proposal for reject test.",
            proposedBudget = 1000,
            proposedTimelineDays = 5,
            milestones = new List<object>
            {
                new { title = "M1", description = "D1", amount = 1000, dueDays = 5, orderIndex = 1, acceptanceCriteria = "A1" }
            }
        };
        var (_, propBody3) = await client.PostAsync($"/api/v1/jobs/{jobId3}/proposals", createProposalReq3);
        var proposalIdStr3 = propBody3?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(proposalIdStr3, out var proposalId3);

        await client.LoginAsClientAsync();
        var (rejRes, rejBody) = await client.PutEmptyAsync($"/api/v1/proposals/{proposalId3}/reject");
        rejRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool rejSuccess = rejBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 2",
            "PUT",
            "/api/v1/proposals/{id}/reject",
            200,
            (int)rejRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: rejSuccess
        );

        // 9. Client accepts proposal (Initializes project)
        var (accRes, accBody) = await client.PutEmptyAsync($"/api/v1/proposals/{proposalId}/accept");
        accRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool accSuccess = accBody?.GetProperty("success").GetBoolean() ?? false;
        var projectIdStr = accBody?.GetProperty("data").GetProperty("projectId").GetString();
        Guid.TryParse(projectIdStr, out var projectId);

        ApiVerificationTracker.Record(
            "Flow 2",
            "PUT",
            "/api/v1/proposals/{id}/accept",
            200,
            (int)accRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: accSuccess && projectId != Guid.Empty
        );

        // 10. Client gets the created project
        var (projRes, projBody) = await client.GetAsync($"/api/v1/projects/{projectId}");
        projRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool projSuccess = projBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 2",
            "GET",
            "/api/v1/projects/{id}",
            200,
            (int)projRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: projSuccess
        );

        // Export all results at the end of the test run
        ApiVerificationTracker.ExportResults();
    }
}
