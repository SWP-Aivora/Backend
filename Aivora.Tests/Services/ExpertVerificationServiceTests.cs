using System.Text;
using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.ExpertVerificationService;
using Aivora.Services.ExpertVerificationService.Providers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class ExpertVerificationServiceTests
{
    private AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    private static (User user, ExpertProfile expert, Skill skill, ExpertSkill expertSkill) SeedExpertSkill(AivoraDbContext dbContext, string skillName = "React")
    {
        var user = new User { Email = $"{Guid.NewGuid()}@test.com", FullName = "Nguyen Van A", Role = UserRole.EXPERT, PasswordHash = "x" };
        var expert = new ExpertProfile { UserId = user.Id };
        var skill = new Skill { Name = skillName };
        var expertSkill = new ExpertSkill { ExpertId = expert.Id, SkillId = skill.Id };

        dbContext.Users.Add(user);
        dbContext.ExpertProfiles.Add(expert);
        dbContext.Skills.Add(skill);
        dbContext.ExpertSkills.Add(expertSkill);
        dbContext.SaveChanges();

        return (user, expert, skill, expertSkill);
    }

    private static IFormFile BuildEvidenceFile(string fileName = "certificate.png")
    {
        var bytes = Encoding.UTF8.GetBytes("fake-evidence-bytes");
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
    }

    private static Aivora.Services.MediaService.IService BuildMediaServiceMock()
    {
        var mock = new Mock<Aivora.Services.MediaService.IService>();
        mock.Setup(x => x.UploadImageAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync(new Aivora.Services.MediaService.Response.UploadResponse { Url = "https://cdn/cert.png", PublicId = "cert123", Format = "png", Bytes = 10 });
        mock.Setup(x => x.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync(new Aivora.Services.MediaService.Response.UploadResponse { Url = "https://cdn/cert.pdf", PublicId = "cert123", Format = "pdf", Bytes = 10 });
        return mock.Object;
    }

    private static IAIExpertVerificationProvider BuildAiProviderMock(ExpertVerificationStatus outcome, decimal confidence = 80)
    {
        var mock = new Mock<IAIExpertVerificationProvider>();
        mock.Setup(x => x.AnalyzeEvidenceAsync(It.IsAny<AnalyzeEvidenceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIVerificationResult { Outcome = outcome, ConfidenceScore = confidence, Reasoning = "Test reasoning" });
        return mock.Object;
    }

    private static IAIExpertVerificationProvider BuildFailingAiProviderMock()
    {
        var mock = new Mock<IAIExpertVerificationProvider>();
        mock.Setup(x => x.AnalyzeEvidenceAsync(It.IsAny<AnalyzeEvidenceRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceUnavailableException("Verification system is busy. Please try again shortly."));
        return mock.Object;
    }

    private static Service BuildService(
        AivoraDbContext dbContext,
        IAIExpertVerificationProvider aiProvider,
        Aivora.Services.MediaService.IService? mediaService = null)
    {
        return new Service(
            dbContext,
            mediaService ?? BuildMediaServiceMock(),
            aiProvider,
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            NullLogger<Service>.Instance);
    }

    [Fact]
    public async Task SubmitEvidenceAsync_SetsIsVerified_WhenAIApproves()
    {
        var dbContext = GetDbContext();
        var (user, _, _, expertSkill) = SeedExpertSkill(dbContext);
        var service = BuildService(dbContext, BuildAiProviderMock(ExpertVerificationStatus.APPROVED));

        var result = await service.SubmitEvidenceAsync(user.Id, new Request.SubmitEvidenceRequest { ExpertSkillId = expertSkill.Id, File = BuildEvidenceFile() });

        result.Status.Should().Be(ExpertVerificationStatus.APPROVED.ToString());
        var updatedSkill = await dbContext.ExpertSkills.FindAsync(expertSkill.Id);
        updatedSkill!.IsVerified.Should().BeTrue();
    }

    [Theory]
    [InlineData(ExpertVerificationStatus.REJECTED)]
    [InlineData(ExpertVerificationStatus.NEEDS_REVIEW)]
    public async Task SubmitEvidenceAsync_DoesNotSetIsVerified_WhenAIDoesNotApprove(ExpertVerificationStatus outcome)
    {
        var dbContext = GetDbContext();
        var (user, _, _, expertSkill) = SeedExpertSkill(dbContext);
        var service = BuildService(dbContext, BuildAiProviderMock(outcome));

        var result = await service.SubmitEvidenceAsync(user.Id, new Request.SubmitEvidenceRequest { ExpertSkillId = expertSkill.Id, File = BuildEvidenceFile() });

        result.Status.Should().Be(outcome.ToString());
        var updatedSkill = await dbContext.ExpertSkills.FindAsync(expertSkill.Id);
        updatedSkill!.IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitEvidenceAsync_NeverFlipsIsVerifiedBackToFalse_AfterLaterRejection()
    {
        var dbContext = GetDbContext();
        var (user, _, _, expertSkill) = SeedExpertSkill(dbContext);

        var approveService = BuildService(dbContext, BuildAiProviderMock(ExpertVerificationStatus.APPROVED));
        await approveService.SubmitEvidenceAsync(user.Id, new Request.SubmitEvidenceRequest { ExpertSkillId = expertSkill.Id, File = BuildEvidenceFile() });

        var rejectService = BuildService(dbContext, BuildAiProviderMock(ExpertVerificationStatus.REJECTED));
        await rejectService.SubmitEvidenceAsync(user.Id, new Request.SubmitEvidenceRequest { ExpertSkillId = expertSkill.Id, File = BuildEvidenceFile() });

        var updatedSkill = await dbContext.ExpertSkills.FindAsync(expertSkill.Id);
        updatedSkill!.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitEvidenceAsync_MakesEscalationEligible_AfterTwoConsecutiveNonApproved()
    {
        var dbContext = GetDbContext();
        var (user, _, _, expertSkill) = SeedExpertSkill(dbContext);

        dbContext.ExpertVerifications.Add(new ExpertVerification
        {
            ExpertSkillId = expertSkill.Id,
            EvidenceFileUrl = "https://cdn/1.png",
            EvidencePublicId = "1",
            Status = ExpertVerificationStatus.REJECTED,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        await dbContext.SaveChangesAsync();

        var service = BuildService(dbContext, BuildAiProviderMock(ExpertVerificationStatus.NEEDS_REVIEW));
        var result = await service.SubmitEvidenceAsync(user.Id, new Request.SubmitEvidenceRequest { ExpertSkillId = expertSkill.Id, File = BuildEvidenceFile() });

        result.CanEscalate.Should().BeTrue();
    }

    [Fact]
    public async Task GetMyVerificationsAsync_ReportsCanEscalate_OnLatestEligibleRecord()
    {
        var dbContext = GetDbContext();
        var (user, _, _, expertSkill) = SeedExpertSkill(dbContext);

        var first = new ExpertVerification
        {
            ExpertSkillId = expertSkill.Id,
            EvidenceFileUrl = "https://cdn/1.png",
            EvidencePublicId = "1",
            Status = ExpertVerificationStatus.REJECTED,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        var second = new ExpertVerification
        {
            ExpertSkillId = expertSkill.Id,
            EvidenceFileUrl = "https://cdn/2.png",
            EvidencePublicId = "2",
            Status = ExpertVerificationStatus.NEEDS_REVIEW,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.ExpertVerifications.AddRange(first, second);
        await dbContext.SaveChangesAsync();

        var service = BuildService(dbContext, BuildAiProviderMock(ExpertVerificationStatus.APPROVED));

        var history = await service.GetMyVerificationsAsync(user.Id, null, new Aivora.Services.Base.Request.PageRequest());

        history.Items.Single(v => v.Id == second.Id).CanEscalate.Should().BeTrue();
        history.Items.Single(v => v.Id == first.Id).CanEscalate.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitEvidenceAsync_ThirdNonApprovedSubmission_RemainsEscalationEligible()
    {
        var dbContext = GetDbContext();
        var (user, _, _, expertSkill) = SeedExpertSkill(dbContext);

        dbContext.ExpertVerifications.AddRange(
            new ExpertVerification
            {
                ExpertSkillId = expertSkill.Id,
                EvidenceFileUrl = "https://cdn/1.png",
                EvidencePublicId = "1",
                Status = ExpertVerificationStatus.REJECTED,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-20)
            },
            new ExpertVerification
            {
                ExpertSkillId = expertSkill.Id,
                EvidenceFileUrl = "https://cdn/2.png",
                EvidencePublicId = "2",
                Status = ExpertVerificationStatus.NEEDS_REVIEW,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            });
        await dbContext.SaveChangesAsync();

        var service = BuildService(dbContext, BuildAiProviderMock(ExpertVerificationStatus.REJECTED));
        var result = await service.SubmitEvidenceAsync(user.Id, new Request.SubmitEvidenceRequest { ExpertSkillId = expertSkill.Id, File = BuildEvidenceFile() });

        result.CanEscalate.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitEvidenceAsync_Throws_WhenExpertSkillDoesNotExist()
    {
        var dbContext = GetDbContext();
        var (user, _, _, _) = SeedExpertSkill(dbContext);
        var service = BuildService(dbContext, BuildAiProviderMock(ExpertVerificationStatus.APPROVED));

        Func<Task> act = () => service.SubmitEvidenceAsync(user.Id, new Request.SubmitEvidenceRequest { ExpertSkillId = Guid.NewGuid(), File = BuildEvidenceFile() });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SubmitEvidenceAsync_Throws_WhenExpertSkillBelongsToAnotherExpert()
    {
        var dbContext = GetDbContext();
        var (_, _, _, expertSkill) = SeedExpertSkill(dbContext, "React");
        var (otherUser, _, _, _) = SeedExpertSkill(dbContext, "Vue");

        var service = BuildService(dbContext, BuildAiProviderMock(ExpertVerificationStatus.APPROVED));

        Func<Task> act = () => service.SubmitEvidenceAsync(otherUser.Id, new Request.SubmitEvidenceRequest { ExpertSkillId = expertSkill.Id, File = BuildEvidenceFile() });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SubmitEvidenceAsync_DoesNotPersistRecord_WhenAIProviderFailsAsInfrastructureError()
    {
        var dbContext = GetDbContext();
        var (user, _, _, expertSkill) = SeedExpertSkill(dbContext);
        var service = BuildService(dbContext, BuildFailingAiProviderMock());

        Func<Task> act = () => service.SubmitEvidenceAsync(user.Id, new Request.SubmitEvidenceRequest { ExpertSkillId = expertSkill.Id, File = BuildEvidenceFile() });

        await act.Should().ThrowAsync<ServiceUnavailableException>();
        (await dbContext.ExpertVerifications.CountAsync()).Should().Be(0);
        var updatedSkill = await dbContext.ExpertSkills.FindAsync(expertSkill.Id);
        updatedSkill!.IsVerified.Should().BeFalse();
    }

    [Fact]
    public async Task EscalateAsync_Throws_WhenNotEligible()
    {
        var dbContext = GetDbContext();
        var (user, _, _, expertSkill) = SeedExpertSkill(dbContext);

        var verification = new ExpertVerification
        {
            ExpertSkillId = expertSkill.Id,
            EvidenceFileUrl = "https://cdn/1.png",
            EvidencePublicId = "1",
            Status = ExpertVerificationStatus.REJECTED
        };
        dbContext.ExpertVerifications.Add(verification);
        await dbContext.SaveChangesAsync();

        var service = BuildService(dbContext, BuildAiProviderMock(ExpertVerificationStatus.APPROVED));

        Func<Task> act = () => service.EscalateAsync(user.Id, verification.Id);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task EscalateAsync_Succeeds_WhenEligible_AndAdminCanApprove()
    {
        var dbContext = GetDbContext();
        var (user, _, _, expertSkill) = SeedExpertSkill(dbContext);

        var first = new ExpertVerification
        {
            ExpertSkillId = expertSkill.Id,
            EvidenceFileUrl = "https://cdn/1.png",
            EvidencePublicId = "1",
            Status = ExpertVerificationStatus.REJECTED,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        var second = new ExpertVerification
        {
            ExpertSkillId = expertSkill.Id,
            EvidenceFileUrl = "https://cdn/2.png",
            EvidencePublicId = "2",
            Status = ExpertVerificationStatus.NEEDS_REVIEW,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.ExpertVerifications.AddRange(first, second);
        await dbContext.SaveChangesAsync();

        var service = BuildService(dbContext, BuildAiProviderMock(ExpertVerificationStatus.APPROVED));

        var escalated = await service.EscalateAsync(user.Id, second.Id);
        escalated.Status.Should().Be(ExpertVerificationStatus.ESCALATED.ToString());

        var adminId = Guid.NewGuid();
        var reviewed = await service.ReviewEscalatedVerificationAsync(adminId, second.Id, new Request.ReviewVerificationRequest { IsApproved = true });

        reviewed.Status.Should().Be(ExpertVerificationStatus.APPROVED.ToString());
        reviewed.AdminId.Should().Be(adminId);
        reviewed.ReviewedAt.Should().NotBeNull();

        var updatedSkill = await dbContext.ExpertSkills.FindAsync(expertSkill.Id);
        updatedSkill!.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task ReviewEscalatedVerificationAsync_Throws_WhenRecordIsNotEscalated()
    {
        var dbContext = GetDbContext();
        var (_, _, _, expertSkill) = SeedExpertSkill(dbContext);

        var verification = new ExpertVerification
        {
            ExpertSkillId = expertSkill.Id,
            EvidenceFileUrl = "https://cdn/1.png",
            EvidencePublicId = "1",
            Status = ExpertVerificationStatus.REJECTED
        };
        dbContext.ExpertVerifications.Add(verification);
        await dbContext.SaveChangesAsync();

        var service = BuildService(dbContext, BuildAiProviderMock(ExpertVerificationStatus.APPROVED));

        Func<Task> act = () => service.ReviewEscalatedVerificationAsync(Guid.NewGuid(), verification.Id, new Request.ReviewVerificationRequest { IsApproved = true });

        await act.Should().ThrowAsync<ValidationException>();
    }
}
