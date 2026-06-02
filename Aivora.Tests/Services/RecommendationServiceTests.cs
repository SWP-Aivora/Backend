using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aivora.Tests.Services;

public class RecommendationServiceTests
{
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
        var service = new Aivora.Services.RecommendationService.Service(dbContext);

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
        var service = new Aivora.Services.RecommendationService.Service(dbContext);

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
        var service = new Aivora.Services.RecommendationService.Service(dbContext);

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
        var service = new Aivora.Services.RecommendationService.Service(dbContext);

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
        var service = new Aivora.Services.RecommendationService.Service(dbContext);

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
        var service = new Aivora.Services.RecommendationService.Service(dbContext);

        Func<Task> wrongOwner = async () => await service.GenerateRecommendationsAsync(Guid.NewGuid(), scenario.JobId);
        await wrongOwner.Should().ThrowAsync<NotFoundException>();

        var job = await dbContext.JobPosts.FindAsync(scenario.JobId);
        job!.Status = JobStatus.DRAFT;
        await dbContext.SaveChangesAsync();

        Func<Task> draftJob = async () => await service.GenerateRecommendationsAsync(scenario.ClientId, scenario.JobId);
        await draftJob.Should().ThrowAsync<ValidationException>();
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
}
