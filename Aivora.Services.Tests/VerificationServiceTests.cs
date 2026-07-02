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
        var expert = TestDataHelper.CreateExpertProfile(expertId, "John Doe");
        await _dbContext.ExpertProfiles.AddAsync(expert);
        await _dbContext.SaveChangesAsync();

        var userContext = new MockUserContext(expertId, "EXPERT");

        // Act
        var result = await _verificationService.StartVerificationAsync(expertId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expertId, result.ExpertId);
        Assert.Equal(VerificationStatus.PENDING, result.Status);
    }
}