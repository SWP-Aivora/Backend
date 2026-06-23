using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Aivora.Repositories.Enums;
using FluentAssertions;
using Xunit;

namespace Aivora.Tests.ApiContract;

public class Flow1JobAndAiApiTests : IClassFixture<ApiContractTestFactory>
{
    private readonly ApiContractTestFactory _factory;

    public Flow1JobAndAiApiTests(ApiContractTestFactory factory)
    {
        _factory = factory;
        _factory.SeedDatabase();
    }

    [Fact]
    public async Task Run_Flow1_API_Verification_Sequence()
    {
        var client = new ApiContractClient(_factory.CreateClient());

        // We must log in as Client
        await client.LoginAsClientAsync();

        // 1.1 POST /api/v1/ai/job-assistant
        var generateReq = new
        {
            rawInput = "Build a deep learning recommendation engine for our streaming platform",
            businessDomain = "Media & Entertainment",
            expectedOutcome = "Increase user engagement by 30% through personalized recommendations",
            budgetType = "FIXED",
            currency = "AICOIN",
            budgetMin = 5000,
            budgetMax = 15000,
            timelineDays = 45,
            experienceLevel = "ADVANCED"
        };
        var (genRes, genBody) = await client.PostAsync("/api/v1/ai/job-assistant", generateReq);
        genRes.StatusCode.Should().Be(HttpStatusCode.Created);

        bool genSuccess = genBody?.GetProperty("success").GetBoolean() ?? false;
        var suggestionData = genBody?.GetProperty("data");
        var suggestionIdStr = suggestionData?.GetProperty("id").GetString();
        Guid.TryParse(suggestionIdStr, out var suggestionId).Should().BeTrue();

        ApiVerificationTracker.Record(
            "Flow 1",
            "POST",
            "/api/v1/ai/job-assistant",
            201,
            (int)genRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: genSuccess && suggestionId != Guid.Empty
        );

        // 1.2 GET /api/v1/ai/job-assistant/{id}
        var (getRes, getBody) = await client.GetAsync($"/api/v1/ai/job-assistant/{suggestionId}");
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool getSuccess = getBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 1",
            "GET",
            "/api/v1/ai/job-assistant/{id}",
            200,
            (int)getRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: getSuccess
        );

        // 1.3 PATCH /api/v1/ai/job-assistant/{id}
        var patchReq = new
        {
            suggestedTitle = "AI Enhanced: Build a recommendation engine",
            experienceLevel = "EXPERT"
        };
        var (patchRes, patchBody) = await client.PatchAsync($"/api/v1/ai/job-assistant/{suggestionId}", patchReq);
        patchRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool patchSuccess = patchBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 1",
            "PATCH",
            "/api/v1/ai/job-assistant/{id}",
            200,
            (int)patchRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: patchSuccess
        );

        // 1.4 POST /api/v1/ai/job-assistant/{id}/refine
        var refineReq = new { message = "Add some more Python details" };
        var (refineRes, refineBody) = await client.PostAsync($"/api/v1/ai/job-assistant/{suggestionId}/refine", refineReq);
        refineRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool refineSuccess = refineBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 1",
            "POST",
            "/api/v1/ai/job-assistant/{id}/refine",
            200,
            (int)refineRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: refineSuccess
        );

        // 1.5 POST /api/v1/ai/job-assistant/{id}/accept
        // Accept and generate job draft
        var acceptReq = new
        {
            categoryId = ApiContractTestData.CategoryId,
            selectedSkillIds = new List<Guid> { ApiContractTestData.SkillId }
        };
        var (acceptRes, acceptBody) = await client.PostAsync($"/api/v1/ai/job-assistant/{suggestionId}/accept", acceptReq);
        acceptRes.StatusCode.Should().Be(HttpStatusCode.Created);
        bool acceptSuccess = acceptBody?.GetProperty("success").GetBoolean() ?? false;
        var jobDraftData = acceptBody?.GetProperty("data").GetProperty("job");
        var jobIdStr = jobDraftData?.GetProperty("id").GetString();
        Guid.TryParse(jobIdStr, out var jobId).Should().BeTrue();

        ApiVerificationTracker.Record(
            "Flow 1",
            "POST",
            "/api/v1/ai/job-assistant/{id}/accept",
            201,
            (int)acceptRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: acceptSuccess && jobId != Guid.Empty
        );

        // 1.6 POST /api/v1/ai/job-assistant/{id}/reject
        // We need a fresh suggestion to reject it
        var (genRes2, genBody2) = await client.PostAsync("/api/v1/ai/job-assistant", generateReq);
        var sugId2 = Guid.Parse(genBody2?.GetProperty("data").GetProperty("id").GetString()!);
        var rejectReq = new { reason = "Decided to do it manually" };
        var (rejectRes, rejectBody) = await client.PostAsync($"/api/v1/ai/job-assistant/{sugId2}/reject", rejectReq);
        rejectRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool rejectSuccess = rejectBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 1",
            "POST",
            "/api/v1/ai/job-assistant/{id}/reject",
            200,
            (int)rejectRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: rejectSuccess
        );

        // 1.7 POST /api/v1/ai/service-generator
        // Service generator requires Expert role
        await client.LoginAsExpertAsync();
        var generateServiceReq = new
        {
            rawInput = "I will build a React frontend",
            skills = new List<string> { "React" },
            priceFrom = 500,
            deliveryDays = 7,
            tone = "professional",
            targetClient = "startup",
            language = "en"
        };
        var (serviceRes, serviceBody) = await client.PostAsync("/api/v1/ai/service-generator", generateServiceReq);
        serviceRes.StatusCode.Should().Be(HttpStatusCode.Created);
        bool serviceSuccess = serviceBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 1",
            "POST",
            "/api/v1/ai/service-generator",
            201,
            (int)serviceRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: serviceSuccess
        );

        // Switch back to Client for Job operations
        await client.LoginAsClientAsync();

        // 1.8 POST /api/v1/jobs
        var createJobReq = new
        {
            title = "Manual Job Post",
            originalDescription = "Building custom backend in .NET",
            categoryId = ApiContractTestData.CategoryId,
            budgetType = "FIXED",
            budgetMin = 1000,
            budgetMax = 2000,
            currency = "AICOIN",
            timelineDays = 30,
            experienceLevel = "ADVANCED",
            visibility = "PUBLIC",
            skillIds = new List<Guid> { ApiContractTestData.SkillId }
        };
        var (createJobRes, createJobBody) = await client.PostAsync("/api/v1/jobs", createJobReq);
        createJobRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool createJobSuccess = createJobBody?.GetProperty("success").GetBoolean() ?? false;
        var manualJobIdStr = createJobBody?.GetProperty("data").GetProperty("id").GetString();
        Guid.TryParse(manualJobIdStr, out var manualJobId).Should().BeTrue();

        ApiVerificationTracker.Record(
            "Flow 1",
            "POST",
            "/api/v1/jobs",
            200,
            (int)createJobRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: createJobSuccess
        );

        // 1.9 PUT /api/v1/jobs/{id}
        var updateJobReq = new
        {
            title = "Manual Job Post - Updated",
            budgetMin = 1200
        };
        var (updateJobRes, updateJobBody) = await client.PutAsync($"/api/v1/jobs/{manualJobId}", updateJobReq);
        updateJobRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool updateJobSuccess = updateJobBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 1",
            "PUT",
            "/api/v1/jobs/{id}",
            200,
            (int)updateJobRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: updateJobSuccess
        );

        // 1.10 POST /api/v1/jobs/{id}/publish
        var (publishRes, publishBody) = await client.PostAsync($"/api/v1/jobs/{manualJobId}/publish", new { });
        publishRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool publishSuccess = publishBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 1",
            "POST",
            "/api/v1/jobs/{id}/publish",
            200,
            (int)publishRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: publishSuccess
        );

        // 1.11 GET /api/v1/jobs
        var (listJobsRes, listJobsBody) = await client.GetAsync("/api/v1/jobs?page=1&pageSize=10");
        listJobsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool listJobsSuccess = listJobsBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 1",
            "GET",
            "/api/v1/jobs",
            200,
            (int)listJobsRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: listJobsSuccess
        );

        // 1.12 GET /api/v1/jobs/{id}
        var (getJobRes, getJobBody) = await client.GetAsync($"/api/v1/jobs/{manualJobId}");
        getJobRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool getJobSuccess = getJobBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 1",
            "GET",
            "/api/v1/jobs/{id}",
            200,
            (int)getJobRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: getJobSuccess
        );

        // 1.13 POST /api/v1/jobs/{id}/cancel
        var (cancelRes, cancelBody) = await client.PostAsync($"/api/v1/jobs/{manualJobId}/cancel", "Budget cuts");
        cancelRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool cancelSuccess = cancelBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 1",
            "POST",
            "/api/v1/jobs/{id}/cancel",
            200,
            (int)cancelRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: cancelSuccess
        );

        // 1.14 DELETE /api/v1/jobs/{id}
        // Create another job to delete it
        var (createJobRes2, createJobBody2) = await client.PostAsync("/api/v1/jobs", createJobReq);
        var delJobIdStr = createJobBody2?.GetProperty("data").GetProperty("id").GetString();
        var (deleteRes, deleteBody) = await client.DeleteAsync($"/api/v1/jobs/{delJobIdStr}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool deleteSuccess = deleteBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 1",
            "DELETE",
            "/api/v1/jobs/{id}",
            200,
            (int)deleteRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: deleteSuccess
        );

        // 1.15 POST /api/v1/jobs/{id}/recommendations/generate
        // We use the job created from the AI suggestion (which is published or we can publish it first)
        await client.PostAsync($"/api/v1/jobs/{jobId}/publish", new { });
        var (genRecRes, genRecBody) = await client.PostAsync($"/api/v1/jobs/{jobId}/recommendations/generate", new { });
        genRecRes.StatusCode.Should().Be(HttpStatusCode.Created);
        bool genRecSuccess = genRecBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 1",
            "POST",
            "/api/v1/jobs/{id}/recommendations/generate",
            201,
            (int)genRecRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: genRecSuccess
        );

        // 1.16 GET /api/v1/jobs/{id}/recommendations
        var (getRecRes, getRecBody) = await client.GetAsync($"/api/v1/jobs/{jobId}/recommendations");
        getRecRes.StatusCode.Should().Be(HttpStatusCode.OK);
        bool getRecSuccess = getRecBody?.GetProperty("success").GetBoolean() ?? false;
        ApiVerificationTracker.Record(
            "Flow 1",
            "GET",
            "/api/v1/jobs/{id}/recommendations",
            200,
            (int)getRecRes.StatusCode,
            requestMatchesDoc: true,
            responseMatchesDoc: getRecSuccess
        );

        // Export all results at the end of the test run
        ApiVerificationTracker.ExportResults();
    }
}
