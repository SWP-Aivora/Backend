using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.VerificationService;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.Tests;

public class VerificationServiceTests
{
    private readonly AivoraDbContext _dbContext;
    private readonly IVerificationService _verificationService;

    public VerificationServiceTests()
    {
        // Configure InMemoryDatabase
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AivoraDbContext(options);
        _verificationService = new MockVerificationService(_dbContext);
    }

    [Fact]
    public async Task StartVerificationAsync_ExpertExists_ReturnsVerification()
    {
        // Arrange
        var expertId = Guid.NewGuid();
        var expert = new ExpertProfile
        {
            Id = expertId,
            User = new User
            {
                Id = expertId,
                FullName = "John Doe",
                Email = $"expert{expertId}@example.com"
            },
            VerificationStatus = VerificationStatus.PENDING
        };
        await _dbContext.ExpertProfiles.AddAsync(expert);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _verificationService.StartVerificationAsync(expertId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expertId, result.ExpertId);
        Assert.Equal(VerificationStatus.PENDING, result.Status);
    }

    [Fact]
    public async Task ScoreProfileAsync_ExpertHasCompleteProfile_ReturnsScore()
    {
        // Arrange
        var expertId = Guid.NewGuid();
        var expert = new ExpertProfile
        {
            Id = expertId,
            User = new User
            {
                Id = expertId,
                FullName = "John Doe",
                Email = $"expert{expertId}@example.com",
                Bio = "Senior AI Engineer with 5 years experience",
                AvatarUrl = "https://example.com/avatar.jpg",
                PhoneNumber = "+1234567890"
            },
            Bio = "Senior AI Engineer with 5 years experience",
            Headline = "AI/ML Specialist",
            HourlyRate = 75,
            VerificationStatus = VerificationStatus.PENDING
        };
        await _dbContext.ExpertProfiles.AddAsync(expert);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _verificationService.ScoreProfileAsync(expertId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result > 0, "Profile score should be greater than 0 for complete profile");
        Assert.True(result <= 100, "Profile score should not exceed 100");
    }

    [Fact]
    public async Task ProcessVerificationAsync_ExpertWithHighScores_ReturnsVerified()
    {
        // Arrange
        var expertId = Guid.NewGuid();
        var expert = new ExpertProfile
        {
            Id = expertId,
            User = new User
            {
                Id = expertId,
                FullName = "John Doe",
                Email = $"expert{expertId}@example.com"
            },
            VerificationStatus = VerificationStatus.PENDING
        };
        await _dbContext.ExpertProfiles.AddAsync(expert);
        await _dbContext.SaveChangesAsync();

        // Start verification first
        await _verificationService.StartVerificationAsync(expertId);

        // Act
        var result = await _verificationService.ProcessVerificationAsync(expertId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(VerificationStatus.VERIFIED, result.Status);
        Assert.True(result.IsPassed);
        Assert.True(result.TotalScore >= 70);
    }

    [Fact]
    public async Task SubmitAppealAsync_RejectedExpert_ReturnsAppealPending()
    {
        // Arrange
        var expertId = Guid.NewGuid();
        var expert = new ExpertProfile
        {
            Id = expertId,
            User = new User
            {
                Id = expertId,
                FullName = "John Doe",
                Email = $"expert{expertId}@example.com"
            },
            VerificationStatus = VerificationStatus.REJECTED
        };
        await _dbContext.ExpertProfiles.AddAsync(expert);
        await _dbContext.SaveChangesAsync();

        // Start and process verification to get rejected
        await _verificationService.StartVerificationAsync(expertId);
        await _verificationService.ProcessVerificationAsync(expertId);

        // Submit appeal
        var appealReason = "I believe my skills assessment was incorrect";
        var result = await _verificationService.SubmitAppealAsync(expertId, appealReason);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(VerificationStatus.APPEAL_PENDING, result.Status);
        Assert.Equal(appealReason, result.AppealReason);
    }
}