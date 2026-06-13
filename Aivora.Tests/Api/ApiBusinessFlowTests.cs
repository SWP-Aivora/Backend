using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aivora.Repositories.Enums;
using FluentAssertions;

namespace Aivora.Tests.Api;

public class ApiBusinessFlowTests
{
    [Fact]
    public async Task ClientCanCreatePublishAndAcceptProposal_ThroughApi()
    {
        await using var factory = new AivoraApiFactory();
        var client = factory.CreateClient();
        var clientSession = await RegisterAsync(client, "CLIENT");
        var expertSession = await RegisterAsync(client, "EXPERT");
        var categoryId = await GetFirstCategoryIdAsync(client);

        client.DefaultRequestHeaders.Authorization = Bearer(clientSession.AccessToken);
        var createJobResponse = await client.PostAsJsonAsync("/api/v1/jobs", new Aivora.Services.JobService.Request.CreateJobRequest
        {
            Title = "API integration job",
            OriginalDescription = "Build an API integration test workflow.",
            CategoryId = categoryId,
            BudgetType = BudgetType.FIXED,
            BudgetMin = 100,
            BudgetMax = 200,
            Currency = "AICOIN",
            TimelineDays = 14,
            Visibility = JobVisibility.PUBLIC,
            Milestones = new List<Aivora.Services.JobService.Request.CreateJobMilestoneRequest>
            {
                new() { Title = "Delivery", Amount = 150, DueDays = 14, OrderIndex = 1 }
            }
        });

        createJobResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var jobId = await ReadDataGuidAsync(createJobResponse, "id");

        var publishResponse = await client.PostAsync($"/api/v1/jobs/{jobId}/publish", null);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        client.DefaultRequestHeaders.Authorization = Bearer(expertSession.AccessToken);
        var proposalResponse = await client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/proposals", new Aivora.Services.ProposalService.Request.CreateProposalRequest
        {
            CoverLetter = "I can deliver this work.",
            ProposedBudget = 150,
            ProposedTimelineDays = 14,
            Milestones = new List<Aivora.Services.ProposalService.Request.CreateProposalMilestoneRequest>
            {
                new() { Title = "Delivery", Amount = 150, DueDays = 14, OrderIndex = 1 }
            }
        });

        proposalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var proposalId = await ReadDataGuidAsync(proposalResponse, "id");

        client.DefaultRequestHeaders.Authorization = Bearer(clientSession.AccessToken);
        var acceptResponse = await client.PutAsync($"/api/v1/proposals/{proposalId}/accept", null);

        acceptResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var projectId = await ReadDataGuidAsync(acceptResponse, "projectId");
        projectId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ProposalEndpoints_EnforceRoleAndOwnershipBoundaries()
    {
        await using var factory = new AivoraApiFactory();
        var client = factory.CreateClient();
        var ownerSession = await RegisterAsync(client, "CLIENT");
        var otherClientSession = await RegisterAsync(client, "CLIENT");
        var expertSession = await RegisterAsync(client, "EXPERT");
        var categoryId = await GetFirstCategoryIdAsync(client);
        var jobId = await CreatePublishedJobAsync(client, ownerSession, categoryId);

        client.DefaultRequestHeaders.Authorization = Bearer(ownerSession.AccessToken);
        var wrongRoleSubmit = await client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/proposals", new
        {
            CoverLetter = "Client should not submit proposals.",
            ProposedBudget = 150
        });
        wrongRoleSubmit.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        client.DefaultRequestHeaders.Authorization = Bearer(expertSession.AccessToken);
        var proposalResponse = await client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/proposals", new Aivora.Services.ProposalService.Request.CreateProposalRequest
        {
            CoverLetter = "Expert proposal",
            ProposedBudget = 150,
            ProposedTimelineDays = 14,
            Milestones = new List<Aivora.Services.ProposalService.Request.CreateProposalMilestoneRequest>
            {
                new() { Title = "Delivery", Amount = 150, DueDays = 14, OrderIndex = 1 }
            }
        });
        proposalResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var proposalId = await ReadDataGuidAsync(proposalResponse, "id");

        client.DefaultRequestHeaders.Authorization = Bearer(otherClientSession.AccessToken);
        var nonOwnerList = await client.GetAsync($"/api/v1/jobs/{jobId}/proposals");
        nonOwnerList.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var nonOwnerAccept = await client.PutAsync($"/api/v1/proposals/{proposalId}/accept", null);
        nonOwnerAccept.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConversationMessages_RejectsNonParticipant()
    {
        await using var factory = new AivoraApiFactory();
        var client = factory.CreateClient();
        var conversationClient = await RegisterAsync(client, "CLIENT");
        var otherClient = await RegisterAsync(client, "CLIENT");
        var expert = await RegisterAsync(client, "EXPERT");

        client.DefaultRequestHeaders.Authorization = Bearer(conversationClient.AccessToken);
        var initResponse = await client.PostAsync($"/api/v1/conversations/init?expertId={expert.UserId}", null);
        initResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var conversationId = await ReadDataGuidAsync(initResponse, "id");

        client.DefaultRequestHeaders.Authorization = Bearer(otherClient.AccessToken);
        var messagesResponse = await client.GetAsync($"/api/v1/conversations/{conversationId}/messages");

        messagesResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<Guid> CreatePublishedJobAsync(HttpClient client, AuthSession ownerSession, Guid categoryId)
    {
        client.DefaultRequestHeaders.Authorization = Bearer(ownerSession.AccessToken);
        var createJobResponse = await client.PostAsJsonAsync("/api/v1/jobs", new Aivora.Services.JobService.Request.CreateJobRequest
        {
            Title = "Ownership API job",
            OriginalDescription = "Build ownership test coverage.",
            CategoryId = categoryId,
            BudgetType = BudgetType.FIXED,
            BudgetMin = 100,
            BudgetMax = 200,
            Currency = "AICOIN",
            TimelineDays = 14,
            Visibility = JobVisibility.PUBLIC
        });

        createJobResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var jobId = await ReadDataGuidAsync(createJobResponse, "id");
        var publishResponse = await client.PostAsync($"/api/v1/jobs/{jobId}/publish", null);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return jobId;
    }

    private static async Task<AuthSession> RegisterAsync(HttpClient client, string role)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = $"api-{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com",
            Password = "Password123!",
            Role = role,
            FullName = $"{role} API Test User"
        });

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");
        return new AuthSession(
            data.GetProperty("userId").GetGuid(),
            data.GetProperty("accessToken").GetString()!);
    }

    private static async Task<Guid> GetFirstCategoryIdAsync(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.GetAsync("/api/v1/categories");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement
            .GetProperty("data")[0]
            .GetProperty("id")
            .GetGuid();
    }

    private static async Task<Guid> ReadDataGuidAsync(HttpResponseMessage response, string propertyName)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement
            .GetProperty("data")
            .GetProperty(propertyName)
            .GetGuid();
    }

    private static AuthenticationHeaderValue Bearer(string accessToken)
    {
        return new AuthenticationHeaderValue("Bearer", accessToken);
    }

    private sealed record AuthSession(Guid UserId, string AccessToken);
}
