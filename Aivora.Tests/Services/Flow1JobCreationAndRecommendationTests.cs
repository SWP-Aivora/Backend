using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.JobService;
using Aivora.Services.Exceptions;
using Aivora.Repositories.Data.Interceptors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

/// <summary>
/// TDD Test Suite for Flow 1: Create Job & Match Expert
/// Focus on observable behaviors through public interfaces
/// </summary>
public class Flow1JobCreationAndRecommendationTests
{
    private AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(new AuditableEntityInterceptor())
            .Options;
        return new AivoraDbContext(options);
    }

    /// <summary>
    /// Test 1: Create Job Draft (Tracer Bullet) - Happy Path
    ///
    /// Behavior Specification:
    /// Given: Client with active account
    /// When: Client creates job with minimal required fields
    /// Then:
    ///   - Job is created with status DRAFT
    ///   - All required fields are populated
    ///   - Job is linked to client
    ///   - System returns complete job response
    /// </summary>
    [Fact]
    public async Task Client_Creates_Job_Draft_Succeeds()
    {
        // ----------------------------------------------------
        // Arrange & Preconditions
        // ----------------------------------------------------
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();

        // Create active client user
        var clientUser = new User
        {
            Id = clientId,
            Email = "client@aivora.com",
            PasswordHash = "hash",
            FullName = "Client User",
            Role = UserRole.CLIENT,
            Status = UserStatus.ACTIVE
        };

        // Create client profile
        var clientProfile = new ClientProfile
        {
            UserId = clientId,
            CompanyName = "Beauty Shop",
            Rating = 0,
            TotalReviews = 0
        };

        // Precondition: Client has active account
        dbContext.Users.Add(clientUser);
        dbContext.ClientProfiles.Add(clientProfile);

        // Create master data
        var category = new Category { Id = Guid.NewGuid(), Name = "AI Chatbots" };
        var skill1 = new Skill { Id = Guid.NewGuid(), Name = "OpenAI API" };
        var skill2 = new Skill { Id = Guid.NewGuid(), Name = "Chatbot" };

        dbContext.Categories.Add(category);
        dbContext.Skills.AddRange(skill1, skill2);
        await dbContext.SaveChangesAsync();

        // ----------------------------------------------------
        // Act: Client creates job draft
        // ----------------------------------------------------
        var jobService = new Service(dbContext, new Aivora.Services.RealtimeService.NullRealtimeService());

        var createJobReq = new Request.CreateJobRequest
        {
            Title = "Build AI Chatbot for Beauty Shop",
            OriginalDescription = "Need chatbot to answer product questions and recommend skincare products",
            CategoryId = category.Id,
            BudgetType = BudgetType.FIXED,
            BudgetMin = 800,
            BudgetMax = 1000,
            TimelineDays = 14,
            Visibility = JobVisibility.PUBLIC,
            SkillIds = new List<Guid> { skill1.Id, skill2.Id }
        };

        var jobResponse = await jobService.CreateJobAsync(clientId, createJobReq);

        // ----------------------------------------------------
        // Assert: Verify observable behavior changes
        // ----------------------------------------------------
        // 1. Job was created with correct status
        jobResponse.Status.Should().Be(JobStatus.DRAFT);
        jobResponse.ClientId.Should().Be(clientId);

        // 2. All required fields populated
        jobResponse.Id.Should().NotBeEmpty();
        jobResponse.Title.Should().Be(createJobReq.Title);
        jobResponse.OriginalDescription.Should().Be(createJobReq.OriginalDescription);
        jobResponse.CategoryId.Should().Be(category.Id);
        jobResponse.BudgetType.Should().Be(BudgetType.FIXED);
        jobResponse.BudgetMin.Should().Be(800);
        jobResponse.BudgetMax.Should().Be(1000);
        jobResponse.TimelineDays.Should().Be(14);
        jobResponse.Visibility.Should().Be(JobVisibility.PUBLIC);
        jobResponse.CreatedAt.Should().NotBe(default);

        // 3. Job is linked to client through database
        var jobInDb = await dbContext.JobPosts.FindAsync(jobResponse.Id);
        jobInDb.Should().NotBeNull();
        jobInDb!.ClientId.Should().Be(clientId);
        jobInDb!.Status.Should().Be(JobStatus.DRAFT);

        // 4. Job skills are properly linked
        var jobSkills = await dbContext.JobSkills
            .Where(js => js.JobId == jobResponse.Id)
            .ToListAsync();
        jobSkills.Should().HaveCount(2);
        jobSkills.Select(js => js.SkillId).Should().Contain(skill1.Id);
        jobSkills.Select(js => js.SkillId).Should().Contain(skill2.Id);

        // 5. System returns complete job response with embedded data
        jobResponse.CategoryName.Should().Be("AI Chatbots");
        jobResponse.Skills.Should().HaveCount(2);
        jobResponse.Skills.Select(s => s.Name).Should().Contain("OpenAI API");
        jobResponse.Skills.Select(s => s.Name).Should().Contain("Chatbot");

        Console.WriteLine("✅ Test passed: Client can create job draft successfully");
    }

    /// <summary>
    /// Test 2: Update Job Draft
    ///
    /// Given: Client has DRAFT job
    /// When: Client updates draft job fields
    /// Then:
    ///   - Job fields are updated
    ///   - Status remains DRAFT
    ///   - Updated timestamp is set
    /// </summary>
    [Fact]
    public async Task Client_Updates_Job_Draft_Succeeds()
    {
        // ----------------------------------------------------
        // Arrange: Create initial DRAFT job
        // ----------------------------------------------------
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();

        var clientUser = new User
        {
            Id = clientId,
            Email = "client@aivora.com",
            PasswordHash = "hash",
            FullName = "Client User",
            Role = UserRole.CLIENT,
            Status = UserStatus.ACTIVE
        };

        var category = new Category { Id = Guid.NewGuid(), Name = "AI Chatbots" };
        var skill = new Skill { Id = Guid.NewGuid(), Name = "OpenAI API" };

        dbContext.Users.Add(clientUser);
        dbContext.Categories.Add(category);
        dbContext.Skills.Add(skill);
        await dbContext.SaveChangesAsync();

        // Create initial DRAFT job
        var jobService = new Service(dbContext, new Aivora.Services.RealtimeService.NullRealtimeService());
        var createJobReq = new Request.CreateJobRequest
        {
            Title = "Initial Title",
            OriginalDescription = "Initial description",
            CategoryId = category.Id,
            BudgetType = BudgetType.FIXED,
            BudgetMin = 500,
            BudgetMax = 700,
            TimelineDays = 10,
            Visibility = JobVisibility.PUBLIC,
            SkillIds = new List<Guid> { skill.Id }
        };

        var jobResponse = await jobService.CreateJobAsync(clientId, createJobReq);
        var jobId = jobResponse.Id;

        // ----------------------------------------------------
        // Act: Update draft job
        // ----------------------------------------------------
        var updateJobReq = new Request.UpdateJobRequest
        {
            Title = "Updated Title - Beauty Chatbot AI",
            FinalDescription = "Need chatbot to answer product questions, recommend skincare products, and handle customer service",
            BudgetMin = 800,
            BudgetMax = 1200,
            TimelineDays = 21
        };

        var updatedJob = await jobService.UpdateJobAsync(clientId, jobId, updateJobReq);

        // ----------------------------------------------------
        // Assert: Verify update behavior
        // ----------------------------------------------------
        // 1. Fields were updated
        updatedJob.Title.Should().Be(updateJobReq.Title);
        updatedJob.FinalDescription.Should().Be(updateJobReq.FinalDescription);
        updatedJob.BudgetMin.Should().Be(800);
        updatedJob.BudgetMax.Should().Be(1200);
        updatedJob.TimelineDays.Should().Be(21);

        // 2. Status remains DRAFT
        updatedJob.Status.Should().Be(JobStatus.DRAFT);

        // 3. Verify database state
        var jobInDb = await dbContext.JobPosts.FindAsync(jobId);
        jobInDb!.Title.Should().Be(updateJobReq.Title);
        jobInDb!.FinalDescription.Should().Be(updateJobReq.FinalDescription);
        jobInDb!.UpdatedAt.Should().NotBeNull();

        Console.WriteLine("✅ Test passed: Client can update job draft successfully");
    }

    /// <summary>
    /// Test 3: Publish Job Draft
    ///
    /// Given: Client has DRAFT job
    /// When: Client publishes the job
    /// Then:
    ///   - Job status changes to OPEN
    ///   - PublishedAt timestamp is set
    ///   - Job becomes visible to experts
    /// </summary>
    [Fact]
    public async Task Client_Publishes_Job_Draft_Succeeds()
    {
        // ----------------------------------------------------
        // Arrange: Create initial DRAFT job
        // ----------------------------------------------------
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();

        var clientUser = new User
        {
            Id = clientId,
            Email = "client@aivora.com",
            PasswordHash = "hash",
            FullName = "Client User",
            Role = UserRole.CLIENT,
            Status = UserStatus.ACTIVE
        };

        var category = new Category { Id = Guid.NewGuid(), Name = "AI Chatbots" };
        var skill = new Skill { Id = Guid.NewGuid(), Name = "OpenAI API" };

        dbContext.Users.Add(clientUser);
        dbContext.Categories.Add(category);
        dbContext.Skills.Add(skill);
        await dbContext.SaveChangesAsync();

        // Create initial DRAFT job
        var jobService = new Service(dbContext, new Aivora.Services.RealtimeService.NullRealtimeService());
        var createJobReq = new Request.CreateJobRequest
        {
            Title = "Build AI Chatbot for Beauty Shop",
            OriginalDescription = "Need chatbot to answer product questions and recommend skincare products",
            CategoryId = category.Id,
            BudgetType = BudgetType.FIXED,
            BudgetMin = 800,
            BudgetMax = 1000,
            TimelineDays = 14,
            Visibility = JobVisibility.PUBLIC,
            SkillIds = new List<Guid> { skill.Id }
        };

        var jobResponse = await jobService.CreateJobAsync(clientId, createJobReq);
        var jobId = jobResponse.Id;

        // Precondition: Job is in DRAFT status
        jobResponse.Status.Should().Be(JobStatus.DRAFT);

        // ----------------------------------------------------
        // Act: Publish the job
        // ----------------------------------------------------
        var publishedJob = await jobService.PublishJobAsync(clientId, jobId);

        // ----------------------------------------------------
        // Assert: Verify publish behavior
        // ----------------------------------------------------
        // 1. Status changed to OPEN
        publishedJob.Status.Should().Be(JobStatus.OPEN);

        // 2. PublishedAt timestamp is set
        publishedJob.PublishedAt.Should().NotBeNull();
        publishedJob.PublishedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));

        // 3. Verify database state
        var jobInDb = await dbContext.JobPosts.FindAsync(jobId);
        jobInDb!.Status.Should().Be(JobStatus.OPEN);
        jobInDb!.PublishedAt.Should().NotBeNull();
        jobInDb!.PublishedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));

        // 4. Job should appear in public job listings
        var publicJobs = await jobService.GetJobsAsync(new Aivora.Services.Base.Request.PageRequest
        {
            PageIndex = 1,
            PageSize = 10
        });

        publicJobs.Items.Should().Contain(j => j.Id == jobId);
        publicJobs.Items.First(j => j.Id == jobId).Status.Should().Be(JobStatus.OPEN);

        Console.WriteLine("✅ Test passed: Client can publish job draft successfully");
    }

    /// <summary>
    /// Test 4: AI Job Assistant Suggestion Generation
    ///
    /// Given: Client wants AI assistance to create a job
    /// When: Client requests AI job suggestion
    /// Then:
    ///   - AI suggestion is generated with structured data
    ///   - Suggestion is saved to database
    ///   - Client can accept the suggestion to create a job
    /// </summary>
    [Fact]
    public async Task Client_Generates_AI_Job_Suggestion_Succeeds()
    {
        // ----------------------------------------------------
        // Arrange: Setup client and master data
        // ----------------------------------------------------
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();

        var clientUser = new User
        {
            Id = clientId,
            Email = "client@aivora.com",
            PasswordHash = "hash",
            FullName = "AI Client",
            Role = UserRole.CLIENT,
            Status = UserStatus.ACTIVE
        };

        var category = new Category { Id = Guid.NewGuid(), Name = "AI Chatbots" };
        var skill = new Skill { Id = Guid.NewGuid(), Name = "OpenAI API" };

        dbContext.Users.Add(clientUser);
        dbContext.Categories.Add(category);
        dbContext.Skills.Add(skill);
        await dbContext.SaveChangesAsync();

        // Setup AI Service with Mock provider
        var mockSuggestionProvider = new Mock<Aivora.Services.AIJobAssistantService.IAIJobSuggestionProvider>();
        var mockRefinementProvider = new Mock<Aivora.Services.AIJobAssistantService.IAIJobRefinementProvider>();
        var mockServiceDescriptionProvider = new Mock<Aivora.Services.AIJobAssistantService.IAIServiceDescriptionProvider>();

        // Create mock AI suggestion
        var mockSuggestion = new Aivora.Services.AIJobAssistantService.AIJobSuggestionDraft
        {
            SuggestedTitle = "Build AI Chatbot for Beauty Shop",
            SuggestedDescription = "Need chatbot to answer product questions and recommend skincare products",
            BusinessDomain = "E-commerce",
            ExpectedOutcome = "Automated customer service with product recommendations",
            BudgetType = BudgetType.FIXED,
            Currency = "AICOIN",
            SuggestedBudgetMin = 800,
            SuggestedBudgetMax = 1000,
            SuggestedTimelineDays = 14,
            ExperienceLevel = SkillLevel.EXPERT,
            SuggestedSkills = new List<string> { "OpenAI API", "Chatbot", "React" },
            SuggestedMilestones = new List<Aivora.Services.AIJobAssistantService.Response.SuggestedMilestone>
            {
                new() { Title = "Chatbot MVP", Amount = 600, DueDays = 10, Description = "Basic chatbot functionality" },
                new() { Title = "Integration & Testing", Amount = 400, DueDays = 14, Description = "Final integration and testing" }
            },
            ClarifyingQuestions = new List<string> { "What is the target platform?", "Any specific integrations required?" },
            ClarifyingAnswers = new List<string> { "Web-based", "CRM integration" },
            RiskWarnings = new List<string> { "API rate limits", "Data privacy compliance" },
            AIModel = "Mock-Gemini"
        };

        mockSuggestionProvider
            .Setup(x => x.GenerateSuggestionAsync(It.IsAny<Aivora.Services.AIJobAssistantService.Request.GenerateSuggestionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockSuggestion);

        // Mock job service
        var mockJobService = new Mock<Aivora.Services.JobService.IService>();
        var createdJob = new Aivora.Services.JobService.Response.JobResponse
        {
            Id = Guid.NewGuid(),
            Title = mockSuggestion.SuggestedTitle,
            OriginalDescription = mockSuggestion.SuggestedDescription,
            FinalDescription = mockSuggestion.SuggestedDescription,
            BusinessDomain = mockSuggestion.BusinessDomain,
            ExpectedOutcome = mockSuggestion.ExpectedOutcome,
            CategoryId = category.Id,
            CategoryName = category.Name,
            BudgetType = mockSuggestion.BudgetType,
            BudgetMin = mockSuggestion.SuggestedBudgetMin,
            BudgetMax = mockSuggestion.SuggestedBudgetMax,
            Currency = mockSuggestion.Currency,
            TimelineDays = mockSuggestion.SuggestedTimelineDays,
            ExperienceLevel = mockSuggestion.ExperienceLevel,
            Status = JobStatus.DRAFT,
            Visibility = JobVisibility.PRIVATE,
            CreatedAt = DateTimeOffset.UtcNow,
            Skills = mockSuggestion.SuggestedSkills.Select(skillName => new Aivora.Services.JobService.Response.SkillInfo
            {
                Id = skill.Id, // Using the skill we created
                Name = skillName
            }).ToList(),
            Milestones = mockSuggestion.SuggestedMilestones.Select((m, index) => new Aivora.Services.JobService.Response.JobMilestoneResponse
            {
                Title = m.Title,
                Description = m.Description,
                Amount = m.Amount,
                DueDays = m.DueDays,
                AcceptanceCriteria = null,
                OrderIndex = index
            }).ToList()
        };

        mockJobService
            .Setup(x => x.CreateJobAsync(clientId, It.IsAny<Aivora.Services.JobService.Request.CreateJobRequest>()))
            .ReturnsAsync(createdJob);

        // Create AI service with mocks
        var aiService = new Aivora.Services.AIJobAssistantService.Service(
            dbContext,
            mockJobService.Object,
            mockSuggestionProvider.Object,
            mockRefinementProvider.Object,
            mockServiceDescriptionProvider.Object,
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));

        // ----------------------------------------------------
        // Act: Generate AI suggestion
        // ----------------------------------------------------
        var generateRequest = new Aivora.Services.AIJobAssistantService.Request.GenerateSuggestionRequest
        {
            RawInput = "Build AI chatbot for beauty shop",
            BusinessDomain = "E-commerce",
            BudgetType = BudgetType.FIXED,
            Currency = "AICOIN",
            BudgetMin = 800,
            BudgetMax = 1000,
            TimelineDays = 14,
            ExperienceLevel = SkillLevel.EXPERT
        };

        var suggestionResponse = await aiService.GenerateSuggestionAsync(clientId, generateRequest);

        // ----------------------------------------------------
        // Assert: Verify AI suggestion generation
        // ----------------------------------------------------
        // 1. Suggestion was generated with correct data
        suggestionResponse.Status.Should().Be(AIJobSuggestionStatus.GENERATED.ToString());
        suggestionResponse.RawInput.Should().Be(generateRequest.RawInput);
        suggestionResponse.SuggestedTitle.Should().Be(mockSuggestion.SuggestedTitle);
        suggestionResponse.SuggestedDescription.Should().Be(mockSuggestion.SuggestedDescription);
        suggestionResponse.SuggestedBudgetMin.Should().Be(800);
        suggestionResponse.SuggestedBudgetMax.Should().Be(1000);
        suggestionResponse.SuggestedTimelineDays.Should().Be(14);
        suggestionResponse.SuggestedSkills.Should().HaveCount(3);
        suggestionResponse.SuggestedSkills.Should().Contain("OpenAI API");
        suggestionResponse.SuggestedMilestones.Should().HaveCount(2);

        // 2. Suggestion is saved to database
        var suggestionInDb = await dbContext.AIJobSuggestions.FindAsync(suggestionResponse.Id);
        suggestionInDb.Should().NotBeNull();
        suggestionInDb!.Status.Should().Be(AIJobSuggestionStatus.GENERATED);
        suggestionInDb!.ClientId.Should().Be(clientId);
        suggestionInDb!.SuggestedTitle.Should().Be(mockSuggestion.SuggestedTitle);

        // 3. Client can accept suggestion to create job
        var acceptRequest = new Aivora.Services.AIJobAssistantService.Request.AcceptSuggestionRequest
        {
            CategoryId = category.Id,
            SelectedSkillIds = new List<Guid> { skill.Id }
        };

        var acceptResult = await aiService.AcceptSuggestionAsync(clientId, suggestionResponse.Id, acceptRequest);

        // 4. Job was created from suggestion
        acceptResult.Job.Should().NotBeNull();
        acceptResult.Job.Title.Should().Be(mockSuggestion.SuggestedTitle);
        acceptResult.Job.Status.Should().Be(JobStatus.DRAFT);
        acceptResult.Job.CategoryId.Should().Be(category.Id);

        // 5. Suggestion status changed to ACCEPTED
        var updatedSuggestion = await dbContext.AIJobSuggestions.FindAsync(suggestionResponse.Id);
        updatedSuggestion!.Status.Should().Be(AIJobSuggestionStatus.ACCEPTED);
        updatedSuggestion!.JobId.Should().Be(acceptResult.Job.Id);

        Console.WriteLine("✅ Test passed: AI Job Assistant suggestion generation works correctly");
    }
}