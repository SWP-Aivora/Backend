using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.MediaService;
using Aivora.Services.VerificationService;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class CertificateServiceTests
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

    private static IFormFile CreateFakeFile(string fileName = "cert.pdf")
    {
        var content = "fake certificate bytes"u8.ToArray();
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }

    [Fact]
    public async Task UploadCertificateAsync_WhenExpertExists_CreatesCertificateRecord()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var expert = await CreateTestExpert(dbContext, Guid.NewGuid());

        var mediaService = new Mock<IService>();
        mediaService
            .Setup(m => m.UploadFileAsync(It.IsAny<IFormFile>(), "certificates"))
            .ReturnsAsync(new Response.UploadResponse
            {
                Url = "https://res.cloudinary.com/fake/raw/upload/certificates/cert.pdf",
                PublicId = "certificates/cert",
                Format = "pdf",
                Bytes = 123
            });

        var service = new CertificateService(dbContext, mediaService.Object);
        var file = CreateFakeFile();

        // Act
        var result = await service.UploadCertificateAsync(expert.Id, file, "AWS Certified", "Amazon");

        // Assert
        result.Should().NotBeNull();
        result.ExpertId.Should().Be(expert.Id);
        result.CertificateName.Should().Be("AWS Certified");
        result.IssuingOrganization.Should().Be("Amazon");
        result.CertificateUrl.Should().Be("https://res.cloudinary.com/fake/raw/upload/certificates/cert.pdf");
        result.IsVerified.Should().BeFalse();
        result.Score.Should().Be(0);

        var dbCertificate = await dbContext.VerificationCertificates
            .FirstOrDefaultAsync(c => c.ExpertId == expert.Id);
        dbCertificate.Should().NotBeNull();
        dbCertificate!.CertificateUrl.Should().Be(result.CertificateUrl);
    }

    [Fact]
    public async Task UploadCertificateAsync_WhenExpertNotExists_ThrowsNotFoundException()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var mediaService = new Mock<IService>();
        var service = new CertificateService(dbContext, mediaService.Object);
        var file = CreateFakeFile();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UploadCertificateAsync(Guid.NewGuid(), file, "AWS Certified", "Amazon"));

        mediaService.Verify(m => m.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetExpertCertificatesAsync_ReturnsOnlyThatExpertsCertificates()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var expertA = await CreateTestExpert(dbContext, Guid.NewGuid());
        var expertB = await CreateTestExpert(dbContext, Guid.NewGuid());

        dbContext.VerificationCertificates.Add(new VerificationCertificate
        {
            ExpertId = expertA.Id,
            CertificateName = "AWS Certified",
            IssuingOrganization = "Amazon",
            CertificateUrl = "https://example.com/a1.pdf",
            IssueDate = DateTime.UtcNow
        });
        dbContext.VerificationCertificates.Add(new VerificationCertificate
        {
            ExpertId = expertB.Id,
            CertificateName = "Azure Certified",
            IssuingOrganization = "Microsoft",
            CertificateUrl = "https://example.com/b1.pdf",
            IssueDate = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var mediaService = new Mock<IService>();
        var service = new CertificateService(dbContext, mediaService.Object);

        // Act
        var result = await service.GetExpertCertificatesAsync(expertA.Id);

        // Assert
        result.Should().ContainSingle();
        result[0].CertificateName.Should().Be("AWS Certified");
        result[0].ExpertId.Should().Be(expertA.Id);
    }

    [Fact]
    public async Task DeleteCertificateAsync_WhenCertificateExists_RemovesItAndReturnsTrue()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var expert = await CreateTestExpert(dbContext, Guid.NewGuid());
        var certificate = new VerificationCertificate
        {
            ExpertId = expert.Id,
            CertificateName = "AWS Certified",
            IssuingOrganization = "Amazon",
            CertificateUrl = "https://example.com/a1.pdf",
            IssueDate = DateTime.UtcNow
        };
        dbContext.VerificationCertificates.Add(certificate);
        await dbContext.SaveChangesAsync();

        var mediaService = new Mock<IService>();
        var service = new CertificateService(dbContext, mediaService.Object);

        // Act
        var result = await service.DeleteCertificateAsync(certificate.Id);

        // Assert
        result.Should().BeTrue();
        (await dbContext.VerificationCertificates.FindAsync(certificate.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteCertificateAsync_WhenCertificateNotFound_ReturnsFalse()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var mediaService = new Mock<IService>();
        var service = new CertificateService(dbContext, mediaService.Object);

        // Act
        var result = await service.DeleteCertificateAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyCertificateAsync_WhenCertificateExists_MarksAsVerified()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var expert = await CreateTestExpert(dbContext, Guid.NewGuid());
        var certificate = new VerificationCertificate
        {
            ExpertId = expert.Id,
            CertificateName = "AWS Certified",
            IssuingOrganization = "Amazon",
            CertificateUrl = "https://example.com/a1.pdf",
            IssueDate = DateTime.UtcNow,
            IsVerified = false,
            Score = 0
        };
        dbContext.VerificationCertificates.Add(certificate);
        await dbContext.SaveChangesAsync();

        var mediaService = new Mock<IService>();
        var service = new CertificateService(dbContext, mediaService.Object);

        // Act
        var result = await service.VerifyCertificateAsync(certificate.Id);

        // Assert
        result.IsVerified.Should().BeTrue();

        var dbCertificate = await dbContext.VerificationCertificates.FindAsync(certificate.Id);
        dbCertificate!.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyCertificateAsync_WhenCertificateNotFound_ThrowsNotFoundException()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var mediaService = new Mock<IService>();
        var service = new CertificateService(dbContext, mediaService.Object);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.VerifyCertificateAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetExpertProfileIdAsync_WhenProfileExists_ReturnsExpertProfileId()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var expert = await CreateTestExpert(dbContext, userId);

        var mediaService = new Mock<IService>();
        var service = new CertificateService(dbContext, mediaService.Object);

        // Act
        var result = await service.GetExpertProfileIdAsync(userId);

        // Assert
        result.Should().Be(expert.Id);
    }

    [Fact]
    public async Task GetExpertProfileIdAsync_WhenProfileNotExists_ThrowsNotFoundException()
    {
        // Arrange
        using var dbContext = GetDbContext();
        var mediaService = new Mock<IService>();
        var service = new CertificateService(dbContext, mediaService.Object);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetExpertProfileIdAsync(Guid.NewGuid()));
    }
}
