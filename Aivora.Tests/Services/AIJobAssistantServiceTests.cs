using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class AIJobAssistantServiceTests
{
    private readonly Mock<Aivora.Services.JobService.IService> _jobServiceMock;

    public AIJobAssistantServiceTests()
    {
        _jobServiceMock = new Mock<Aivora.Services.JobService.IService>();
    }

    private AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    [Fact]
    public async Task GenerateSuggestionAsync_Succeeds_AndSavesToDb()
    {
        // Arrange
        var dbContext = GetDbContext();
        var aiService = new Aivora.Services.AIJobAssistantService.Service(dbContext, _jobServiceMock.Object);
        var clientId = Guid.NewGuid();
        var request = new Aivora.Services.AIJobAssistantService.Request.GenerateSuggestionRequest
        {
            RawInput = "I want a simple React website with authentication.",
            BudgetMin = 1000,
            BudgetMax = 2000,
            TimelineDays = 10
        };

        // Act
        var result = await aiService.GenerateSuggestionAsync(clientId, request);

        // Assert
        result.Should().NotBeNull();
        result.RawInput.Should().Be(request.RawInput);
        result.SuggestedBudgetMin.Should().Be(1000);
        result.Status.Should().Be(AIJobSuggestionStatus.GENERATED.ToString());

        var dbSuggestion = await dbContext.AIJobSuggestions.FindAsync(result.Id);
        dbSuggestion.Should().NotBeNull();
        dbSuggestion!.ClientId.Should().Be(clientId);
    }

    [Fact]
    public async Task AcceptSuggestionAsync_CreatesJob_AndUpdatesStatus()
    {
        // Arrange
        var dbContext = GetDbContext();
        var aiService = new Aivora.Services.AIJobAssistantService.Service(dbContext, _jobServiceMock.Object);
        var clientId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();
        
        var suggestion = new AIJobSuggestion
        {
            Id = suggestionId,
            ClientId = clientId,
            RawInput = "Input",
            Status = AIJobSuggestionStatus.GENERATED,
            SuggestedBudgetMin = 500,
            SuggestedBudgetMax = 1000,
            SuggestedTimelineDays = 7
        };
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        var jobId = Guid.NewGuid();
        _jobServiceMock.Setup(x => x.CreateJobAsync(It.IsAny<Guid>(), It.IsAny<Aivora.Services.JobService.Request.CreateJobRequest>()))
            .ReturnsAsync(new Aivora.Services.JobService.Response.JobResponse { Id = jobId, Status = JobStatus.DRAFT });

        var acceptRequest = new Aivora.Services.AIJobAssistantService.Request.AcceptSuggestionRequest { CategoryId = Guid.NewGuid() };

        // Act
        var result = await aiService.AcceptSuggestionAsync(clientId, suggestionId, acceptRequest);

        // Assert
        result.Job.Id.Should().Be(jobId);
        var updatedSuggestion = await dbContext.AIJobSuggestions.FindAsync(suggestionId);
        updatedSuggestion!.Status.Should().Be(AIJobSuggestionStatus.ACCEPTED);
        updatedSuggestion.JobId.Should().Be(jobId);
    }

    [Fact]
    public async Task AcceptSuggestionAsync_ThrowsValidation_WhenAlreadyProcessed()
    {
        // Arrange
        var dbContext = GetDbContext();
        var aiService = new Aivora.Services.AIJobAssistantService.Service(dbContext, _jobServiceMock.Object);
        var clientId = Guid.NewGuid();
        var suggestion = new AIJobSuggestion { ClientId = clientId, Status = AIJobSuggestionStatus.ACCEPTED, RawInput = "x" };
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        // Act
        Func<Task> act = async () => await aiService.AcceptSuggestionAsync(clientId, suggestion.Id, new());

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RejectSuggestionAsync_UpdatesStatusToRejected()
    {
        // Arrange
        var dbContext = GetDbContext();
        var aiService = new Aivora.Services.AIJobAssistantService.Service(dbContext, _jobServiceMock.Object);
        var clientId = Guid.NewGuid();
        var suggestionId = Guid.NewGuid();
        
        var suggestion = new AIJobSuggestion { Id = suggestionId, ClientId = clientId, RawInput = "Input", Status = AIJobSuggestionStatus.GENERATED };
        dbContext.AIJobSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await aiService.RejectSuggestionAsync(clientId, suggestionId, new());

        // Assert
        result.Should().BeTrue();
        var updatedSuggestion = await dbContext.AIJobSuggestions.FindAsync(suggestionId);
        updatedSuggestion!.Status.Should().Be(AIJobSuggestionStatus.REJECTED);
    }
}
