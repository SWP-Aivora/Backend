using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.VerificationService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Tests.Services;

public class VerificationService_TDD
{
    private AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    // Tracer Bullet Test: Expert có thể start verification process
    [Fact]
    public async Task StartVerificationAsync_ExpertExists_CreatesVerificationRecord()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var (expertId, _, _) = await CreateTestExpert(dbContext);
        var service = new Aivora.Services.VerificationService.Service(dbContext);

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

    // Test: Expert có thể restart verification process
    [Fact]
    public async Task StartVerificationAsync_WhenVerificationExists_UpdatesExistingRecord()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var (expertId, _, _) = await CreateTestExpert(dbContext);

        // Update expert to REJECTED status for this test
        var expert = await dbContext.ExpertProfiles.FindAsync(expertId);
        expert!.VerificationStatus = VerificationStatus.REJECTED;
        await dbContext.SaveChangesAsync();

        // Create existing verification
        var existingVerification = new ExpertVerification
        {
            Id = Guid.NewGuid(),
            ExpertId = expertId,
            Status = VerificationStatus.REJECTED,
            TotalScore = 50,
            RetryCount = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.ExpertVerifications.Add(existingVerification);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.VerificationService.Service(dbContext);

        // Act
        var result = await service.StartVerificationAsync(expertId);

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
        dbVerification.UpdatedAt.Should().NotBeNull();
    }

    // Test: Expert không tồn tại → throw NotFoundException
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

    // Helper method to create test expert
    private async Task<(Guid, User, ExpertProfile)> CreateTestExpert(AivoraDbContext dbContext)
    {
        var expertId = Guid.NewGuid();

        var user = new User
        {
            Id = expertId,
            FullName = "Test Expert",
            Email = "expert@test.com",
            Role = UserRole.EXPERT,
            PasswordHash = "hash"
        };
        dbContext.Users.Add(user);

        var expert = new ExpertProfile
        {
            Id = expertId,
            UserId = expertId,
            Title = "Software Developer",
            Bio = "Experienced developer",
            ExperienceYears = 5,
            AvailabilityStatus = AvailabilityStatus.AVAILABLE,
            VerificationStatus = VerificationStatus.PENDING
        };
        dbContext.ExpertProfiles.Add(expert);
        await dbContext.SaveChangesAsync();

        return (expertId, user, expert);
    }

    // Test: Expert có thể submit appeal khi bị rejected
    [Fact]
    public async Task SubmitAppealAsync_WhenExpertRejected_SubmitsAppeal()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var (expertId, _, _) = await CreateTestExpert(dbContext);

        // Create and reject verification first
        var verification = new ExpertVerification
        {
            Id = Guid.NewGuid(),
            ExpertId = expertId,
            Status = VerificationStatus.REJECTED,
            TotalScore = 50,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.ExpertVerifications.Add(verification);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.VerificationService.Service(dbContext);

        // Act
        var appealReason = "I believe my application was unfairly rejected";
        var result = await service.SubmitAppealAsync(expertId, appealReason);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(VerificationStatus.APPEAL_PENDING);
        result.AppealReason.Should().Be(appealReason);
        result.AppealRequestedAt.Should().NotBeNull();

        // Verify database updated
        var dbVerification = await dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);
        dbVerification!.Status.Should().Be(VerificationStatus.APPEAL_PENDING);
        dbVerification.AppealReason.Should().Be(appealReason);
    }

    // Test: Expert không thể submit appeal khi không bị rejected
    [Fact]
    public async Task SubmitAppealAsync_WhenExpertNotRejected_ThrowsException()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var (expertId, _, _) = await CreateTestExpert(dbContext);

        // Create verification with PENDING status
        var verification = new ExpertVerification
        {
            Id = Guid.NewGuid(),
            ExpertId = expertId,
            Status = VerificationStatus.PENDING,
            TotalScore = 0,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ExpertVerifications.Add(verification);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.VerificationService.Service(dbContext);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.SubmitAppealAsync(expertId, "Some reason"));
    }

    // Test: Admin có thể approve appeal
    [Fact]
    public async Task ReviewAppealAsync_WhenApproved_ApprovesAppeal()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var (expertId, _, _) = await CreateTestExpert(dbContext);

        // Create verification and submit appeal
        var verification = new ExpertVerification
        {
            Id = Guid.NewGuid(),
            ExpertId = expertId,
            Status = VerificationStatus.APPEAL_PENDING,
            AppealReason = "I believe my application was unfairly rejected",
            AppealRequestedAt = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.ExpertVerifications.Add(verification);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.VerificationService.Service(dbContext);

        // Act
        var adminFeedback = "Appeal approved after review";
        var result = await service.ReviewAppealAsync(verification.Id, true, adminFeedback);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(VerificationStatus.VERIFIED);
        result.IsPassed.Should().BeTrue();
        result.AppealAdminFeedback.Should().Be(adminFeedback);

        // Verify database updated
        var dbVerification = await dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);
        dbVerification!.Status.Should().Be(VerificationStatus.VERIFIED);
        dbVerification.IsPassed.Should().BeTrue();
        dbVerification.AppealAdminFeedback.Should().Be(adminFeedback);
    }

    // Test: Admin có thể reject appeal
    [Fact]
    public async Task ReviewAppealAsync_WhenRejected_Reappeal()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var (expertId, _, _) = await CreateTestExpert(dbContext);

        // Create verification and submit appeal
        var verification = new ExpertVerification
        {
            Id = Guid.NewGuid(),
            ExpertId = expertId,
            Status = VerificationStatus.APPEAL_PENDING,
            AppealReason = "I believe my application was unfairly rejected",
            AppealRequestedAt = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.ExpertVerifications.Add(verification);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.VerificationService.Service(dbContext);

        // Act
        var adminFeedback = "Appeal rejected - insufficient evidence";
        var result = await service.ReviewAppealAsync(verification.Id, false, adminFeedback);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(VerificationStatus.REJECTED);
        result.IsPassed.Should().BeFalse();
        result.AppealAdminFeedback.Should().Be(adminFeedback);

        // Verify database updated
        var dbVerification = await dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);
        dbVerification!.Status.Should().Be(VerificationStatus.REJECTED);
        dbVerification.IsPassed.Should().BeFalse();
        dbVerification.AppealAdminFeedback.Should().Be(adminFeedback);
    }

    // Test: Admin có thể approve verification trực tiếp
    [Fact]
    public async Task AdminApproveAsync_ApprovesExpert()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var (expertId, _, _) = await CreateTestExpert(dbContext);

        // Create verification with REJECTED status
        var verification = new ExpertVerification
        {
            Id = Guid.NewGuid(),
            ExpertId = expertId,
            Status = VerificationStatus.REJECTED,
            TotalScore = 50,
            RetryCount = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.ExpertVerifications.Add(verification);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.VerificationService.Service(dbContext);

        // Act
        var result = await service.AdminApproveAsync(expertId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(VerificationStatus.VERIFIED);
        result.IsPassed.Should().BeTrue();
        result.Feedback.Should().Contain("Approved by administrator");
        result.RetryCount.Should().Be(0); // Should reset retry count

        // Verify database updated
        var dbVerification = await dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);
        dbVerification!.Status.Should().Be(VerificationStatus.VERIFIED);
        dbVerification.IsPassed.Should().BeTrue();
        dbVerification.Feedback.Should().Contain("Approved by administrator");
    }

    // Test: Admin có thể reject verification với lý do
    [Fact]
    public async Task AdminRejectAsync_RejectsExpert()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var (expertId, _, _) = await CreateTestExpert(dbContext);

        // Create verification with PENDING status
        var verification = new ExpertVerification
        {
            Id = Guid.NewGuid(),
            ExpertId = expertId,
            Status = VerificationStatus.PENDING,
            TotalScore = 0,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ExpertVerifications.Add(verification);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.VerificationService.Service(dbContext);

        // Act
        var rejectionReason = "Insufficient documentation and experience";
        var result = await service.AdminRejectAsync(expertId, rejectionReason);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(VerificationStatus.REJECTED);
        result.IsPassed.Should().BeFalse();
        result.FailureReason.Should().Be(rejectionReason);
        result.Feedback.Should().Contain("Verification rejected");
        result.Feedback.Should().Contain(rejectionReason);

        // Verify database updated
        var dbVerification = await dbContext.ExpertVerifications
            .FirstOrDefaultAsync(v => v.ExpertId == expertId);
        dbVerification!.Status.Should().Be(VerificationStatus.REJECTED);
        dbVerification.IsPassed.Should().BeFalse();
        dbVerification.FailureReason.Should().Be(rejectionReason);
    }
}