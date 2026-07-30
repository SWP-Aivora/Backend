using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.AIJobRefinementService;
using Aivora.Services.Exceptions;
using Aivora.Services.Options;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class AIJobRefinementServiceTests
{
    private readonly Mock<IAIJobRefinementProvider> _refinementProviderMock = new();
    private readonly Mock<Aivora.Services.JobService.IService> _jobServiceMock = new();

    private static AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    private static User SetupClient(AivoraDbContext dbContext)
    {
        var client = new User { FullName = "Test Client", Email = "test@test.com", PasswordHash = "hash" };
        dbContext.Users.Add(client);
        dbContext.SaveChanges();
        return client;
    }

    private Service CreateService(AivoraDbContext dbContext)
    {
        return new Service(dbContext, _jobServiceMock.Object, _refinementProviderMock.Object, Options.Create(new ExchangeRateOptions()));
    }

    [Fact]
    public async Task RefineJobAsync_WithBudgetChange_UpdatesJobAndReturnsChangedFields()
    {
        var dbContext = GetDbContext();
        var client = SetupClient(dbContext);
        var service = CreateService(dbContext);

        var job = new JobPost
        {
            ClientId = client.Id,
            Client = client,
            Title = "Test Job",
            OriginalDescription = "Original description",
            CategoryId = Guid.NewGuid(),
            BudgetType = BudgetType.FIXED,
            Currency = "AICOIN",
            BudgetMin = 500,
            BudgetMax = 1000,
            Status = JobStatus.DRAFT
        };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        _refinementProviderMock
            .Setup(x => x.RefineJobAsync(It.IsAny<Aivora.Services.JobService.Response.JobResponse>(), "change budget to 2000", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIJobRefinementDraft
            {
                Title = job.Title,
                BudgetMin = 1000,
                BudgetMax = 2000,
                Currency = "AICOIN",
                AIResponse = "Budget updated."
            });

        _jobServiceMock
            .Setup(x => x.GetJobByIdAsync(job.Id))
            .ReturnsAsync(new Aivora.Services.JobService.Response.JobResponse { Id = job.Id, Title = job.Title, Status = JobStatus.DRAFT });

        var result = await service.RefineJobAsync(client.Id, job.Id,
            new Request.RefineJobRequest { Message = "change budget to 2000" });

        result.ChangedFields.Should().Contain(new[] { "budgetMin", "budgetMax" });
        result.AIResponse.Should().Be("Budget updated.");
    }

    [Fact]
    public async Task RefineJobAsync_WithUsdBudget_ConvertsToAicoin()
    {
        var dbContext = GetDbContext();
        var client = SetupClient(dbContext);
        var service = CreateService(dbContext);

        var job = new JobPost
        {
            ClientId = client.Id,
            Client = client,
            Title = "Test Job",
            OriginalDescription = "Original description",
            CategoryId = Guid.NewGuid(),
            BudgetType = BudgetType.FIXED,
            Currency = "AICOIN",
            BudgetMin = 500,
            BudgetMax = 1000,
            Status = JobStatus.DRAFT
        };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        _refinementProviderMock
            .Setup(x => x.RefineJobAsync(It.IsAny<Aivora.Services.JobService.Response.JobResponse>(), "change budget to 100 usd", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIJobRefinementDraft
            {
                Title = job.Title,
                BudgetMin = 100,
                Currency = "USD",
                AIResponse = "Budget updated."
            });

        _jobServiceMock
            .Setup(x => x.GetJobByIdAsync(job.Id))
            .ReturnsAsync(new Aivora.Services.JobService.Response.JobResponse { Id = job.Id, Title = job.Title, Status = JobStatus.DRAFT });

        await service.RefineJobAsync(client.Id, job.Id,
            new Request.RefineJobRequest { Message = "change budget to 100 usd" });

        var updatedJob = await dbContext.JobPosts.FindAsync(job.Id);
        updatedJob!.Currency.Should().Be("AICOIN");
        updatedJob.BudgetMin.Should().Be(100 * 25);
    }

    [Fact]
    public async Task RefineJobAsync_WithHallucinatedCurrency_DoesNotThrowAndKeepsCurrentCurrency()
    {
        var dbContext = GetDbContext();
        var client = SetupClient(dbContext);
        var service = CreateService(dbContext);

        var job = new JobPost
        {
            ClientId = client.Id,
            Client = client,
            Title = "Test Job",
            OriginalDescription = "Original description",
            CategoryId = Guid.NewGuid(),
            Currency = "AICOIN",
            Status = JobStatus.DRAFT
        };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        // "EUR" is outside the configured rate table (AICOIN/USD/VND) — an AI hallucination,
        // not something the client asked to convert.
        _refinementProviderMock
            .Setup(x => x.RefineJobAsync(It.IsAny<Aivora.Services.JobService.Response.JobResponse>(), "what currency should I use?", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIJobRefinementDraft
            {
                Title = job.Title,
                Currency = "EUR",
                AIResponse = "Advice only."
            });

        _jobServiceMock
            .Setup(x => x.GetJobByIdAsync(job.Id))
            .ReturnsAsync(new Aivora.Services.JobService.Response.JobResponse { Id = job.Id, Title = job.Title, Status = JobStatus.DRAFT });

        Func<Task> act = async () => await service.RefineJobAsync(client.Id, job.Id,
            new Request.RefineJobRequest { Message = "what currency should I use?" });

        await act.Should().NotThrowAsync();
        var updatedJob = await dbContext.JobPosts.FindAsync(job.Id);
        updatedJob!.Currency.Should().Be("AICOIN");
    }

    [Fact]
    public async Task RefineJobAsync_WithEmptySkillsList_DoesNotClearExistingSkills()
    {
        var dbContext = GetDbContext();
        var client = SetupClient(dbContext);
        var service = CreateService(dbContext);

        var skill = new Skill { Name = "React" };
        dbContext.Skills.Add(skill);
        var job = new JobPost
        {
            ClientId = client.Id,
            Client = client,
            Title = "Test Job",
            OriginalDescription = "Original description",
            CategoryId = Guid.NewGuid(),
            Currency = "AICOIN",
            Status = JobStatus.DRAFT
        };
        job.JobSkills.Add(new JobSkill { Job = job, Skill = skill });
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        // The AI didn't mention skills at all — its draft's Skills list defaults to empty.
        // That must mean "not mentioned", not "clear every skill".
        _refinementProviderMock
            .Setup(x => x.RefineJobAsync(It.IsAny<Aivora.Services.JobService.Response.JobResponse>(), "rename to Updated Title", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIJobRefinementDraft
            {
                Title = "Updated Title",
                AIResponse = "Title updated."
            });

        _jobServiceMock
            .Setup(x => x.GetJobByIdAsync(job.Id))
            .ReturnsAsync(new Aivora.Services.JobService.Response.JobResponse { Id = job.Id, Title = "Updated Title", Status = JobStatus.DRAFT });

        var result = await service.RefineJobAsync(client.Id, job.Id,
            new Request.RefineJobRequest { Message = "rename to Updated Title" });

        result.ChangedFields.Should().NotContain("skills");
        var updatedJob = await dbContext.JobPosts.Include(j => j.JobSkills).FirstAsync(j => j.Id == job.Id);
        updatedJob.JobSkills.Should().ContainSingle(js => js.SkillId == skill.Id);
    }

    [Fact]
    public async Task RefineJobAsync_AdvisoryMessage_DoesNotChangeJob()
    {
        var dbContext = GetDbContext();
        var client = SetupClient(dbContext);
        var service = CreateService(dbContext);

        var job = new JobPost
        {
            ClientId = client.Id,
            Client = client,
            Title = "Test Job",
            OriginalDescription = "Original description",
            CategoryId = Guid.NewGuid(),
            Status = JobStatus.DRAFT
        };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        _refinementProviderMock
            .Setup(x => x.RefineJobAsync(It.IsAny<Aivora.Services.JobService.Response.JobResponse>(), "explain budget", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIJobRefinementDraft
            {
                Title = job.Title,
                AIResponse = "Advice only."
            });

        _jobServiceMock
            .Setup(x => x.GetJobByIdAsync(job.Id))
            .ReturnsAsync(new Aivora.Services.JobService.Response.JobResponse { Id = job.Id, Title = job.Title, Status = JobStatus.DRAFT });

        var result = await service.RefineJobAsync(client.Id, job.Id,
            new Request.RefineJobRequest { Message = "explain budget" });

        result.ChangedFields.Should().BeEmpty();
        result.AIResponse.Should().Be("Advice only.");
    }

    [Fact]
    public async Task RefineJobAsync_NotOwner_ThrowsNotFoundException()
    {
        var dbContext = GetDbContext();
        SetupClient(dbContext);
        var service = CreateService(dbContext);

        var job = new JobPost
        {
            ClientId = Guid.NewGuid(),
            Title = "Test Job",
            OriginalDescription = "Original description",
            CategoryId = Guid.NewGuid(),
            Status = JobStatus.DRAFT
        };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        Func<Task> act = async () => await service.RefineJobAsync(Guid.NewGuid(), job.Id,
            new Request.RefineJobRequest { Message = "change something" });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RefineJobAsync_ShortMessage_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var service = CreateService(dbContext);

        Func<Task> act = async () => await service.RefineJobAsync(Guid.NewGuid(), Guid.NewGuid(),
            new Request.RefineJobRequest { Message = "ab" });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Message must be at least 3 characters long.");
    }

    [Fact]
    public async Task RefineJobAsync_NonEditableStatus_ThrowsValidationException()
    {
        var dbContext = GetDbContext();
        var client = SetupClient(dbContext);
        var service = CreateService(dbContext);

        var job = new JobPost
        {
            ClientId = client.Id,
            Client = client,
            Title = "Test Job",
            OriginalDescription = "Original description",
            CategoryId = Guid.NewGuid(),
            Status = JobStatus.CANCELLED
        };
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync();

        Func<Task> act = async () => await service.RefineJobAsync(client.Id, job.Id,
            new Request.RefineJobRequest { Message = "change something" });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Job cannot be refined in its current status.");
    }
}
