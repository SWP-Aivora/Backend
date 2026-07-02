using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.AIJobAssistantService;
using Aivora.Services.Exceptions;
using Aivora.Services.VerificationService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
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

    private async Task<ExpertProfile> CreateTestExpert(AivoraDbContext dbContext, Guid userId)
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
        await dbContext.SaveChangesAsync();

        return expert;
    }

    // Test 1: RED - Expert có thể start verification process
    [Fact]
    public async Task StartVerificationAsync_WhenExpertExists_CreatesVerificationRecord()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var userId = Guid.NewGuid();

        // Create test expert
        var expert = await CreateTestExpert(dbContext, userId);

        // Mock AI providers (simplified for testing)
        var service = new Aivora.Services.VerificationService.Service(dbContext);

        // Act
        var result = await service.StartVerificationAsync(expert.Id);

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
            .FirstOrDefaultAsync(v => v.ExpertId == expert.Id);
        dbVerification.Should().NotBeNull();
        dbVerification!.Status.Should().Be(VerificationStatus.PENDING);
    }

    [Fact]
    public async Task StartVerificationAsync_WhenExpertNotExists_ThrowsException()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var service = new Aivora.Services.VerificationService.Service(dbContext);
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
        var userId = Guid.NewGuid();

        var expert = await CreateTestExpert(dbContext, userId);

        // Create existing verification
        var existingVerification = new ExpertVerification
        {
            Id = Guid.NewGuid(),
            ExpertId = expert.Id,
            Status = VerificationStatus.REJECTED,
            TotalScore = 50,
            RetryCount = 1
        };
        dbContext.ExpertVerifications.Add(existingVerification);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.VerificationService.Service(dbContext);

        // Act
        var result = await service.StartVerificationAsync(expert.Id);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(VerificationStatus.PENDING);
        result.TotalScore.Should().Be(0);
        result.RetryCount.Should().Be(0); // Should reset retry count

        // Verify existing record updated
        var dbVerification = await dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expert.Id);
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

        await CreateTestExpert(dbContext, userId);

        var verification = new ExpertVerification
        {
            ExpertId = expertId,
            Status = VerificationStatus.PENDING,
            TotalScore = 0
        };
        dbContext.ExpertVerifications.Add(verification);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.VerificationService.Service(dbContext);

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
        var service = new Aivora.Services.VerificationService.Service(dbContext);
        var nonExistentExpertId = Guid.NewGuid();

        // Act
        var result = await service.GetVerificationAsync(nonExistentExpertId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ProcessVerificationAsync_WhenExpertExists_UpdatesVerificationStatus()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var userId = Guid.NewGuid();

        var expert = await CreateTestExpert(dbContext, userId);

        // Create verification record
        var verification = new ExpertVerification
        {
            ExpertId = expert.Id,
            Status = VerificationStatus.PENDING,
            TotalScore = 0,
            ProcessingStatus = "queued"
        };
        dbContext.ExpertVerifications.Add(verification);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.VerificationService.Service(dbContext);

        // Act
        var result = await service.ProcessVerificationAsync(expert.Id);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(VerificationStatus.VERIFIED, VerificationStatus.REJECTED);
        result.TotalScore.Should().BeGreaterThan(0);
        result.ProcessingStatus.Should().Be("completed");
        result.IsPassed.Should().Be(result.Status == VerificationStatus.VERIFIED);
    }

    [Fact]
    public async Task ProcessVerificationAsync_WhenVerificationNotExists_ThrowsException()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var service = new Aivora.Services.VerificationService.Service(dbContext);
        var nonExistentExpertId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.ProcessVerificationAsync(nonExistentExpertId));
    }

    [Fact]
    public async Task SubmitAppealAsync_WhenVerificationRejected_SubmitsAppeal()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var userId = Guid.NewGuid();

        var expert = await CreateTestExpert(dbContext, userId);

        // Create rejected verification
        var verification = new ExpertVerification
        {
            ExpertId = expert.Id,
            Status = VerificationStatus.REJECTED,
            TotalScore = 50,
            AppealReason = null
        };
        dbContext.ExpertVerifications.Add(verification);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.VerificationService.Service(dbContext);

        // Act
        var result = await service.SubmitAppealAsync(expert.Id, "I disagree with the rejection");

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(VerificationStatus.APPEAL_PENDING);
        result.AppealReason.Should().Be("I disagree with the rejection");
        result.AppealRequestedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SubmitAppealAsync_WhenVerificationNotExists_ThrowsException()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var service = new Aivora.Services.VerificationService.Service(dbContext);
        var nonExistentExpertId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.SubmitAppealAsync(nonExistentExpertId, "Test reason"));
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenAppealApproved_VerifiesExpert()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var userId = Guid.NewGuid();

        var expert = await CreateTestExpert(dbContext, userId);

        // Create appeal
        var verification = new ExpertVerification
        {
            Id = Guid.NewGuid(),
            ExpertId = expert.Id,
            Status = VerificationStatus.APPEAL_PENDING,
            AppealReason = "I disagree with the rejection",
            IsPassed = false
        };
        dbContext.ExpertVerifications.Add(verification);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.VerificationService.Service(dbContext);

        // Act
        var result = await service.ReviewAppealAsync(verification.Id, true, "Appeal approved");

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(VerificationStatus.VERIFIED);
        result.IsPassed.Should().Be(true);
        result.AppealAdminFeedback.Should().Be("Appeal approved");
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenAppealRejected_ExpertRemainsRejected()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var userId = Guid.NewGuid();

        var expert = await CreateTestExpert(dbContext, userId);

        // Create appeal
        var verification = new ExpertVerification
        {
            Id = Guid.NewGuid(),
            ExpertId = expert.Id,
            Status = VerificationStatus.APPEAL_PENDING,
            AppealReason = "I disagree with the rejection",
            IsPassed = false
        };
        dbContext.ExpertVerifications.Add(verification);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.VerificationService.Service(dbContext);

        // Act
        var result = await service.ReviewAppealAsync(verification.Id, false, "Appeal rejected");

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(VerificationStatus.REJECTED);
        result.IsPassed.Should().Be(false);
        result.AppealAdminFeedback.Should().Be("Appeal rejected");
    }

    [Fact]
    public async Task ReviewAppealAsync_WhenAppealNotExists_ThrowsException()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var service = new Aivora.Services.VerificationService.Service(dbContext);
        var nonExistentAppealId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.ReviewAppealAsync(nonExistentAppealId, true, "Test feedback"));
    }
}