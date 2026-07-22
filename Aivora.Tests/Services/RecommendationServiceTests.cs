using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.Options;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aivora.Tests.Services;

public class RecommendationServiceTests
{
    private static readonly IOptions<RecommendationOptions> DefaultOptions = Options.Create(new RecommendationOptions());
    private static AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AivoraDbContext(options);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_UsesWeightedSkillLevels()
    {
        var dbContext = GetDbContext();
        var scenario = SeedScenario(dbContext, BudgetType.HOURLY, budgetMin: 10, budgetMax: 100, timelineDays: 10);
        var beginner = AddExpert(dbContext, scenario.Skill, SkillLevel.BEGINNER, hourlyRate: 25, rating: 4, successRate: 80);
        var expert = AddExpert(dbContext, scenario.Skill, SkillLevel.EXPERT, hourlyRate: 25, rating: 4, successRate: 80);
        await dbContext.SaveChangesAsync();
        var service = new Aivora.Services.RecommendationService.Service(dbContext, DefaultOptions, new Aivora.Services.RecommendationService.Providers.MockExpertRecommendationProvider());

        var results = await service.GenerateRecommendationsAsync(scenario.ClientId, scenario.JobId);

        results.Single(x => x.ExpertId == expert.UserId).SkillScore.Should().BeGreaterThan(results.Single(x => x.ExpertId == beginner.UserId).SkillScore);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ScoresHourlyBudget()
    {
        var dbContext = GetDbContext();
        var scenario = SeedScenario(dbContext, BudgetType.HOURLY, budgetMin: 50, budgetMax: 100, timelineDays: 10);
        var expert = AddExpert(dbContext, scenario.Skill, SkillLevel.EXPERT, hourlyRate: 75, rating: 4, successRate: 80);
        await dbContext.SaveChangesAsync();
        var service = new Aivora.Services.RecommendationService.Service(dbContext, DefaultOptions, new Aivora.Services.RecommendationService.Providers.MockExpertRecommendationProvider());

        var results = await service.GenerateRecommendationsAsync(scenario.ClientId, scenario.JobId);

        results.Single(x => x.ExpertId == expert.UserId).BudgetScore.Should().Be(100);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ScoresFixedBudgetFromTimelineAndHourlyRate()
    {
        var dbContext = GetDbContext();
        var scenario = SeedScenario(dbContext, BudgetType.FIXED, budgetMin: 500, budgetMax: 600, timelineDays: 2);
        var expert = AddExpert(dbContext, scenario.Skill, SkillLevel.EXPERT, hourlyRate: 50, rating: 4, successRate: 80);
        await dbContext.SaveChangesAsync();
        var service = new Aivora.Services.RecommendationService.Service(dbContext, DefaultOptions, new Aivora.Services.RecommendationService.Providers.MockExpertRecommendationProvider());

        var results = await service.GenerateRecommendationsAsync(scenario.ClientId, scenario.JobId);

        results.Single(x => x.ExpertId == expert.UserId).BudgetScore.Should().Be(100);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ScoresAvailabilityRatingAndCompletion()
    {
        var dbContext = GetDbContext();
        var scenario = SeedScenario(dbContext, BudgetType.HOURLY, budgetMin: 10, budgetMax: 100, timelineDays: 10);
        var expert = AddExpert(dbContext, scenario.Skill, SkillLevel.EXPERT, hourlyRate: 25, rating: 4.5m, successRate: 92, availability: AvailabilityStatus.BUSY);
        await dbContext.SaveChangesAsync();
        var service = new Aivora.Services.RecommendationService.Service(dbContext, DefaultOptions, new Aivora.Services.RecommendationService.Providers.MockExpertRecommendationProvider());

        var results = await service.GenerateRecommendationsAsync(scenario.ClientId, scenario.JobId);
        var result = results.Single(x => x.ExpertId == expert.UserId);

        result.RatingScore.Should().Be(90);
        result.AvailabilityScore.Should().Be(50);
        result.CompletionScore.Should().Be(92);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_PersistsAllScoreComponents()
    {
        var dbContext = GetDbContext();
        var scenario = SeedScenario(dbContext, BudgetType.HOURLY, budgetMin: 10, budgetMax: 100, timelineDays: 10);
        var expert = AddExpert(dbContext, scenario.Skill, SkillLevel.EXPERT, hourlyRate: 25, rating: 5, successRate: 100);
        await dbContext.SaveChangesAsync();
        var service = new Aivora.Services.RecommendationService.Service(dbContext, DefaultOptions, new Aivora.Services.RecommendationService.Providers.MockExpertRecommendationProvider());

        await service.GenerateRecommendationsAsync(scenario.ClientId, scenario.JobId);

        var saved = await dbContext.RecommendationResults.SingleAsync(x => x.ExpertId == expert.UserId);
        saved.SkillScore.Should().Be(100);
        saved.BudgetScore.Should().Be(100);
        saved.RatingScore.Should().Be(100);
        saved.AvailabilityScore.Should().Be(100);
        saved.CompletionScore.Should().Be(100);
        saved.PortfolioScore.Should().Be(0);
        saved.TotalScore.Should().Be(100);
        saved.Explanation.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_KeepsClientOwnershipAndOpenJobRequirement()
    {
        var dbContext = GetDbContext();
        var scenario = SeedScenario(dbContext, BudgetType.HOURLY, budgetMin: 10, budgetMax: 100, timelineDays: 10);
        AddExpert(dbContext, scenario.Skill, SkillLevel.EXPERT, hourlyRate: 25, rating: 5, successRate: 100);
        await dbContext.SaveChangesAsync();
        var service = new Aivora.Services.RecommendationService.Service(dbContext, DefaultOptions, new Aivora.Services.RecommendationService.Providers.MockExpertRecommendationProvider());

        Func<Task> wrongOwner = async () => await service.GenerateRecommendationsAsync(Guid.NewGuid(), scenario.JobId);
        await wrongOwner.Should().ThrowAsync<NotFoundException>();

        var job = await dbContext.JobPosts.FindAsync(scenario.JobId);
        job!.Status = JobStatus.DRAFT;
        await dbContext.SaveChangesAsync();

    }

    [Fact]
    public async Task GenerateRecommendationsAsync_NoPenaltyWhenUnderThreeCompletedProjects()
    {
        var dbContext = GetDbContext();
        var scenario = SeedScenario(dbContext, BudgetType.HOURLY, budgetMin: 10, budgetMax: 100, timelineDays: 10);
        var expert = AddExpert(dbContext, scenario.Skill, SkillLevel.EXPERT, hourlyRate: 25, rating: 5, successRate: 100);
        expert.CompletedProjects = 2; // Under 3
        AddDispute(dbContext, expert.UserId); // 1 dispute, rate = 50%, but shouldn't penalize because projects < 3
        await dbContext.SaveChangesAsync();
        var service = new Aivora.Services.RecommendationService.Service(dbContext, DefaultOptions, new Aivora.Services.RecommendationService.Providers.MockExpertRecommendationProvider());

        var results = await service.GenerateRecommendationsAsync(scenario.ClientId, scenario.JobId);
        var result = results.Single(x => x.ExpertId == expert.UserId);

        result.DisputePenalty.Should().Be(0);
        result.DisputeRate.Should().Be(0);
        result.TotalScore.Should().Be(100);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_NoPenaltyWhenNoDisputes()
    {
        var dbContext = GetDbContext();
        var scenario = SeedScenario(dbContext, BudgetType.HOURLY, budgetMin: 10, budgetMax: 100, timelineDays: 10);
        var expert = AddExpert(dbContext, scenario.Skill, SkillLevel.EXPERT, hourlyRate: 25, rating: 5, successRate: 100);
        expert.CompletedProjects = 5;
        // 0 disputes
        await dbContext.SaveChangesAsync();
        var service = new Aivora.Services.RecommendationService.Service(dbContext, DefaultOptions, new Aivora.Services.RecommendationService.Providers.MockExpertRecommendationProvider());

        var results = await service.GenerateRecommendationsAsync(scenario.ClientId, scenario.JobId);
        var result = results.Single(x => x.ExpertId == expert.UserId);

        result.DisputePenalty.Should().Be(0);
        result.DisputeRate.Should().Be(0);
        result.TotalScore.Should().Be(100);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_AppliesPenaltyForDisputes()
    {
        var dbContext = GetDbContext();
        var scenario = SeedScenario(dbContext, BudgetType.HOURLY, budgetMin: 10, budgetMax: 100, timelineDays: 10);
        var expert = AddExpert(dbContext, scenario.Skill, SkillLevel.EXPERT, hourlyRate: 25, rating: 5, successRate: 100);
        expert.CompletedProjects = 5;
        AddDispute(dbContext, expert.UserId); // 1 dispute, rate = 1/5 = 0.2
        await dbContext.SaveChangesAsync();
        var service = new Aivora.Services.RecommendationService.Service(dbContext, DefaultOptions, new Aivora.Services.RecommendationService.Providers.MockExpertRecommendationProvider());

        var results = await service.GenerateRecommendationsAsync(scenario.ClientId, scenario.JobId);
        var result = results.Single(x => x.ExpertId == expert.UserId);

        result.DisputeRate.Should().Be(0.2m);
        result.DisputePenalty.Should().Be(0.3m); // 0.2 * 1.5 = 0.3
        result.TotalScore.Should().Be(70); // 100 * (1 - 0.3)
        result.Explanation.Should().Contain("dispute");
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_CapsPenaltyAt50Percent()
    {
        var dbContext = GetDbContext();
        var scenario = SeedScenario(dbContext, BudgetType.HOURLY, budgetMin: 10, budgetMax: 100, timelineDays: 10);
        var expert = AddExpert(dbContext, scenario.Skill, SkillLevel.EXPERT, hourlyRate: 25, rating: 5, successRate: 100);
        expert.CompletedProjects = 5;
        AddDispute(dbContext, expert.UserId);
        AddDispute(dbContext, expert.UserId);
        AddDispute(dbContext, expert.UserId); // 3 disputes, rate = 3/5 = 0.6
        await dbContext.SaveChangesAsync();
        var service = new Aivora.Services.RecommendationService.Service(dbContext, DefaultOptions, new Aivora.Services.RecommendationService.Providers.MockExpertRecommendationProvider());

        var results = await service.GenerateRecommendationsAsync(scenario.ClientId, scenario.JobId);
        var result = results.Single(x => x.ExpertId == expert.UserId);

        result.DisputeRate.Should().Be(0.6m);
        result.DisputePenalty.Should().Be(0.5m); // 0.6 * 1.5 = 0.9, cap at 0.5
        result.TotalScore.Should().Be(50); // 100 * (1 - 0.5)
        result.Explanation.Should().Contain("dispute");
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_AppliesOverduePenalty_WhenExpertHasOverdueMilestones()
    {
        var dbContext = GetDbContext();
        var scenario = SeedScenario(dbContext, BudgetType.HOURLY, budgetMin: 10, budgetMax: 100, timelineDays: 10);
        var expert = AddExpert(dbContext, scenario.Skill, SkillLevel.EXPERT, hourlyRate: 25, rating: 5, successRate: 100);
        expert.CompletedProjects = 5;

        // Seed 2 active milestones: 1 overdue (yesterday), 1 on-time (tomorrow)
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        AddProjectWithMilestones(dbContext, expert.UserId,
            (MilestoneStatus.FUNDED, yesterday),
            (MilestoneStatus.IN_PROGRESS, tomorrow)
        );

        await dbContext.SaveChangesAsync();
        var service = new Aivora.Services.RecommendationService.Service(dbContext, DefaultOptions, new Aivora.Services.RecommendationService.Providers.MockExpertRecommendationProvider());

        var results = await service.GenerateRecommendationsAsync(scenario.ClientId, scenario.JobId);
        var result = results.Single(x => x.ExpertId == expert.UserId);

        result.OverdueRate.Should().Be(0.5m);
        result.OverduePenalty.Should().Be(0.15m); // 0.5 * 0.3 = 0.15
        result.TotalScore.Should().Be(85m); // 100 * (1 - 0.15)
        result.Explanation.Should().Contain("overdue");
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_NoOverduePenalty_WhenNoOverdueMilestones()
    {
        var dbContext = GetDbContext();
        var scenario = SeedScenario(dbContext, BudgetType.HOURLY, budgetMin: 10, budgetMax: 100, timelineDays: 10);
        var expert = AddExpert(dbContext, scenario.Skill, SkillLevel.EXPERT, hourlyRate: 25, rating: 5, successRate: 100);
        expert.CompletedProjects = 5;

        // Seed 1 active milestone: on-time (tomorrow)
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        AddProjectWithMilestones(dbContext, expert.UserId,
            (MilestoneStatus.FUNDED, tomorrow)
        );

        await dbContext.SaveChangesAsync();
        var service = new Aivora.Services.RecommendationService.Service(dbContext, DefaultOptions, new Aivora.Services.RecommendationService.Providers.MockExpertRecommendationProvider());

        var results = await service.GenerateRecommendationsAsync(scenario.ClientId, scenario.JobId);
        var result = results.Single(x => x.ExpertId == expert.UserId);

        result.OverdueRate.Should().Be(0m);
        result.OverduePenalty.Should().Be(0m);
        result.TotalScore.Should().Be(100m);
    }

    [Fact]
    public async Task GenerateRecommendationsAsync_ShouldOnlyAggregateMilestonesForActiveOrDisputedProjects()
    {
        var dbContext = GetDbContext();
        var scenario = SeedScenario(dbContext, BudgetType.HOURLY, budgetMin: 10, budgetMax: 100, timelineDays: 10);
        var expert = AddExpert(dbContext, scenario.Skill, SkillLevel.EXPERT, hourlyRate: 25, rating: 5, successRate: 100);
        expert.CompletedProjects = 5;

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        // 1. ACTIVE project -> 1 overdue, 1 on-time milestone (Total = 2, Overdue = 1)
        AddProjectWithMilestonesAndStatus(dbContext, expert.UserId, ProjectStatus.ACTIVE,
            (MilestoneStatus.FUNDED, yesterday),
            (MilestoneStatus.IN_PROGRESS, tomorrow));
        // 2. DISPUTED project -> 1 overdue milestone (Total = 1, Overdue = 1)
        AddProjectWithMilestonesAndStatus(dbContext, expert.UserId, ProjectStatus.DISPUTED,
            (MilestoneStatus.FUNDED, yesterday));
        // 3. CANCELLED project -> 1 overdue milestone (Total = 1, Overdue = 1) - should NOT be counted
        AddProjectWithMilestonesAndStatus(dbContext, expert.UserId, ProjectStatus.CANCELLED,
            (MilestoneStatus.FUNDED, yesterday));
        // 4. COMPLETED project -> 1 overdue milestone (Total = 1, Overdue = 1) - should NOT be counted
        AddProjectWithMilestonesAndStatus(dbContext, expert.UserId, ProjectStatus.COMPLETED,
            (MilestoneStatus.FUNDED, yesterday));

        await dbContext.SaveChangesAsync();
        var service = new Aivora.Services.RecommendationService.Service(dbContext, DefaultOptions, new Aivora.Services.RecommendationService.Providers.MockExpertRecommendationProvider());

        var results = await service.GenerateRecommendationsAsync(scenario.ClientId, scenario.JobId);
        var result = results.Single(x => x.ExpertId == expert.UserId);

        // We seeded 5 milestones, but only 3 belong to ACTIVE or DISPUTED projects.
        // So total count should be 3, overdue count should be 2.
        // Overdue rate = 2 / 3 = 0.6667 (66.67%)
        result.OverdueRate.Should().Be(0.6667m);
    }

    [Fact]
    public void RecommendationResult_Properties_ShouldHavePrecision18AndScale4()
    {
        var dbContext = GetDbContext();
        var entityType = dbContext.Model.FindEntityType(typeof(RecommendationResult));

        entityType.Should().NotBeNull();

        var overdueRate = entityType!.FindProperty(nameof(RecommendationResult.OverdueRate));
        overdueRate.Should().NotBeNull();
        overdueRate!.GetPrecision().Should().Be(18);
        overdueRate!.GetScale().Should().Be(4);

        var overduePenalty = entityType.FindProperty(nameof(RecommendationResult.OverduePenalty));
        overduePenalty.Should().NotBeNull();
        overduePenalty!.GetPrecision().Should().Be(18);
        overduePenalty!.GetScale().Should().Be(4);

        var disputeRate = entityType.FindProperty(nameof(RecommendationResult.DisputeRate));
        disputeRate.Should().NotBeNull();
        disputeRate!.GetPrecision().Should().Be(18);
        disputeRate!.GetScale().Should().Be(4);

        var disputePenalty = entityType.FindProperty(nameof(RecommendationResult.DisputePenalty));
        disputePenalty.Should().NotBeNull();
        disputePenalty!.GetPrecision().Should().Be(18);
        disputePenalty!.GetScale().Should().Be(4);
    }

    private static void AddProjectWithMilestonesAndStatus(
        AivoraDbContext dbContext,
        Guid expertUserId,
        ProjectStatus projectStatus,
        params (MilestoneStatus Status, DateOnly? DueDate)[] milestones)
    {
        var client = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@test.local",
            PasswordHash = "hash",
            FullName = "Client",
            Role = UserRole.CLIENT,
            Status = UserStatus.ACTIVE
        };
        var project = new Project
        {
            Id = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            ClientId = client.Id,
            ExpertId = expertUserId,
            Title = "Project",
            Status = projectStatus,
            TotalBudget = 100
        };
        dbContext.Users.Add(client);
        dbContext.Projects.Add(project);

        int index = 1;
        foreach (var m in milestones)
        {
            var milestone = new Milestone
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Title = $"Milestone {index}",
                Amount = 50,
                Status = m.Status,
                DueDate = m.DueDate,
                OrderIndex = index++
            };
            dbContext.Milestones.Add(milestone);
        }
    }


    private static void AddProjectWithMilestones(
        AivoraDbContext dbContext,
        Guid expertUserId,
        params (MilestoneStatus Status, DateOnly? DueDate)[] milestones)
    {
        var client = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@test.local",
            PasswordHash = "hash",
            FullName = "Client",
            Role = UserRole.CLIENT,
            Status = UserStatus.ACTIVE
        };
        var project = new Project
        {
            Id = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            ClientId = client.Id,
            ExpertId = expertUserId,
            Title = "Project",
            Status = ProjectStatus.ACTIVE,
            TotalBudget = 100
        };
        dbContext.Users.Add(client);
        dbContext.Projects.Add(project);

        int index = 1;
        foreach (var m in milestones)
        {
            var milestone = new Milestone
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Title = $"Milestone {index}",
                Amount = 50,
                Status = m.Status,
                DueDate = m.DueDate,
                OrderIndex = index++
            };
            dbContext.Milestones.Add(milestone);
        }
    }

    private static Scenario SeedScenario(AivoraDbContext dbContext, BudgetType budgetType, decimal budgetMin, decimal budgetMax, int timelineDays)
    {
        var clientId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var skill = new Skill { Id = Guid.NewGuid(), Name = "React" };
        var job = new JobPost
        {
            Id = jobId,
            ClientId = clientId,
            Title = "Build app",
            OriginalDescription = "Build app",
            BudgetType = budgetType,
            BudgetMin = budgetMin,
            BudgetMax = budgetMax,
            Currency = "USD",
            TimelineDays = timelineDays,
            Status = JobStatus.OPEN,
            Visibility = JobVisibility.PUBLIC
        };
        var jobSkill = new JobSkill { JobId = jobId, Job = job, SkillId = skill.Id, Skill = skill };

        dbContext.Skills.Add(skill);
        dbContext.JobPosts.Add(job);
        dbContext.JobSkills.Add(jobSkill);

        return new Scenario(clientId, jobId, skill);
    }

    private static ExpertProfile AddExpert(
        AivoraDbContext dbContext,
        Skill skill,
        SkillLevel level,
        decimal hourlyRate,
        decimal rating,
        decimal successRate,
        AvailabilityStatus availability = AvailabilityStatus.AVAILABLE)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@test.local",
            PasswordHash = "hash",
            FullName = "Expert",
            Role = UserRole.EXPERT,
            Status = UserStatus.ACTIVE
        };
        var profile = new ExpertProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Title = "AI Expert",
            HourlyRate = hourlyRate,
            Rating = rating,
            SuccessRate = successRate,
            AvailabilityStatus = availability,
            CompletedProjects = 10
        };
        var expertSkill = new ExpertSkill
        {
            ExpertId = profile.Id,
            Expert = profile,
            SkillId = skill.Id,
            Skill = skill,
            Level = level
        };

        user.ExpertProfile = profile;

        dbContext.Users.Add(user);
        dbContext.ExpertProfiles.Add(profile);
        dbContext.ExpertSkills.Add(expertSkill);

        return profile;
    }

    private sealed record Scenario(Guid ClientId, Guid JobId, Skill Skill);
    private static void AddDispute(AivoraDbContext dbContext, Guid expertUserId)
    {
        var client = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid()}@test.local",
            PasswordHash = "hash",
            FullName = "Client",
            Role = UserRole.CLIENT,
            Status = UserStatus.ACTIVE
        };
        var project = new Project
        {
            Id = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            ClientId = client.Id,
            ExpertId = expertUserId,
            Title = "Project",
            Status = ProjectStatus.PENDING_PAYMENT,
            TotalBudget = 100
        };
        var milestone = new Milestone
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Title = "M1",
            Amount = 100,
            Status = MilestoneStatus.CREATED,
            OrderIndex = 1
        };
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            MilestoneId = milestone.Id,
            PayerId = client.Id,
            PayeeId = expertUserId,
            Amount = 100,
            Status = PaymentStatus.PENDING
        };
        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            MilestoneId = milestone.Id,
            PaymentId = payment.Id,
            OpenedBy = client.Id,
            AgainstUserId = expertUserId,
            Reason = "Test",
            Status = DisputeStatus.OPEN
        };

        dbContext.Users.Add(client);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.Payments.Add(payment);
        dbContext.Disputes.Add(dispute);
    }
}
