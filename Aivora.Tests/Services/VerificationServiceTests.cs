using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.VerificationService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aivora.Tests.Services;

public class VerificationServiceTests
{
    private AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    private ExpertProfile CreateTestExpert(AivoraDbContext dbContext, Guid userId)
    {
        var user = new User
        {
            Id = userId,
            FullName = "Test Expert",
            Email = "expert@test.com",
            Role = UserRole.EXPERT,
            PasswordHash = "hash"
        };
        dbContext.Users.Add(user);

        var expert = new ExpertProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Software Developer",
            Bio = "Experienced developer",
            ExperienceYears = 5,
            AvailabilityStatus = AvailabilityStatus.AVAILABLE,
            VerificationStatus = VerificationStatus.PENDING
        };
        dbContext.ExpertProfiles.Add(expert);
        dbContext.SaveChangesAsync();

        return expert;
    }

    // Test 1: RED - Expert có thể start verification process
    [Fact]
    public async Task StartVerificationAsync_WhenExpertExists_CreatesVerificationRecord()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var expertId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Create test expert
        CreateTestExpert(dbContext, userId);

        // Mock AI provider (simplified for testing)
        var mockAiProvider = new MockAIJobSuggestionProvider();
        var mockAiServiceProvider = new MockAIServiceDescriptionProvider();

        var service = new Service(dbContext, mockAiProvider, mockAiServiceProvider);

        // Act
        var result = await service.StartVerificationAsync(expertId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(VerificationStatus.PENDING);
        result.TotalScore.Should().Be(0);
        result.CurrentStage.Should().Be(0);
        result.ProcessingStatus.Should().Be("queued");
        result.RetryCount.Should().Be(0);
        result.MaxRetries.Should().Be(3);

        // Verify database record created
        var dbVerification = await dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);
        dbVerification.Should().NotBeNull();
        dbVerification!.Status.Should().Be(VerificationStatus.PENDING);
    }

    [Fact]
    public async Task StartVerificationAsync_WhenExpertNotExists_ThrowsException()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var mockAiProvider = new MockAIJobSuggestionProvider();
        var mockAiServiceProvider = new MockAIServiceDescriptionProvider();

        var service = new Service(dbContext, mockAiProvider, mockAiServiceProvider);
        var nonExistentExpertId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.StartVerificationAsync(nonExistentExpertId));
    }

    [Fact]
    public async Task StartVerificationAsync_WhenVerificationExists_UpdatesExistingRecord()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var expertId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        CreateTestExpert(dbContext, userId);

        // Create existing verification
        var existingVerification = new ExpertVerification
        {
            ExpertId = expertId,
            Status = VerificationStatus.REJECTED,
            TotalScore = 50,
            RetryCount = 1
        };
        dbContext.ExpertVerifications.Add(existingVerification);
        await dbContext.SaveChangesAsync();

        var mockAiProvider = new MockAIJobSuggestionProvider();
        var mockAiServiceProvider = new MockAIServiceDescriptionProvider();

        var service = new Service(dbContext, mockAiProvider, mockAiServiceProvider);

        // Act
        var result = await service.StartVerificationAsync(expertId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(VerificationStatus.PENDING);
        result.TotalScore.Should().Be(0);
        result.RetryCount.Should().Be(0); // Should reset retry count

        // Verify existing record updated
        var dbVerification = await dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);
        dbVerification!.Status.Should().Be(VerificationStatus.PENDING);
        dbVerification.RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task GetVerificationAsync_WhenExists_ReturnsVerification()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var expertId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        CreateTestExpert(dbContext, userId);

        var verification = new ExpertVerification
        {
            ExpertId = expertId,
            Status = VerificationStatus.PENDING,
            TotalScore = 0
        };
        dbContext.ExpertVerifications.Add(verification);
        await dbContext.SaveChangesAsync();

        var mockAiProvider = new MockAIJobSuggestionProvider();
        var mockAiServiceProvider = new MockAIServiceDescriptionProvider();

        var service = new Service(dbContext, mockAiProvider, mockAiServiceProvider);

        // Act
        var result = await service.GetVerificationAsync(expertId);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(VerificationStatus.PENDING);
        result.TotalScore.Should().Be(0);
    }

    [Fact]
    public async Task GetVerificationAsync_WhenNotExists_ReturnsNull()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var mockAiProvider = new MockAIJobSuggestionProvider();
        var mockAiServiceProvider = new MockAIServiceDescriptionProvider();

        var service = new Service(dbContext, mockAiProvider, mockAiServiceProvider);
        var nonExistentExpertId = Guid.NewGuid();

        // Act
        var result = await service.GetVerificationAsync(nonExistentExpertId);

        // Assert
        result.Should().BeNull();
    }
}

// Mock AI providers for testing
public class MockAIJobSuggestionProvider : Aivora.Services.AIJobAssistantService.Providers.IAIJobSuggestionProvider
{
    public Task<Aivora.Services.AIJobAssistantService.Response.AIJobSuggestionResponse> SuggestJobAsync(string prompt)
    {
        return Task.FromResult(new Aivora.Services.AIJobAssistantService.Response.AIJobSuggestionResponse
        {
            Suggestion = "Mock AI response for testing",
            Score = 80
        });
    }
}

public class MockAIServiceDescriptionProvider : Aivora.Services.AIJobAssistantService.Providers.IAIServiceDescriptionProvider
{
    public Task<Aivora.Services.AIJobAssistantService.Response.AIServiceDescriptionResponse> GetServiceDescriptionAsync(string prompt)
    {
        return Task.FromResult(new Aivora.Services.AIJobAssistantService.Response.AIServiceDescriptionResponse
        {
            Description = "Mock AI response for testing",
            Suggestion = "Mock suggestion"
        });
    }
}