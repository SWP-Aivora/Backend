using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.MilestoneService;
using Aivora.Services.Treasury;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Aivora.Services.Options;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class MilestoneServiceTests
{
    private AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    [Fact]
    public async Task FundMilestoneAsync_Succeeds_WhenBalanceIsEnough()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var wallet = new Wallet { UserId = clientId, AvailableBalance = 1000, Currency = "AICOIN" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 0, Currency = "AICOIN" };
        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.PENDING_PAYMENT, Currency = "AICOIN" };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Amount = 300, Status = MilestoneStatus.CREATED, Title = "Milestone 1", Currency = "AICOIN" };

        dbContext.Wallets.AddRange(wallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        // Finance setup
        var commissionOptions = Options.Create(new CommissionOptions { Rate = 0.10m });
        var treasury = new Aivora.Services.Treasury.Treasury(dbContext, new Aivora.Services.Treasury.CommissionCalculator(commissionOptions), Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var service = new Service(dbContext, treasury, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());

        // Act
        var result = await service.FundMilestoneAsync(clientId, milestoneId);

        // Assert
        result.Milestone.Status.Should().Be(MilestoneStatus.IN_PROGRESS);
        result.Wallet.AvailableBalance.Should().Be(910); // 1000 - 30% of 300
        result.Wallet.HeldBalance.Should().Be(0);

        var updatedProject = await dbContext.Projects.FindAsync(projectId);
        updatedProject!.Status.Should().Be(ProjectStatus.ACTIVE);

        var payment = await dbContext.Payments.FirstOrDefaultAsync(p => p.MilestoneId == milestoneId);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.RELEASED);
        payment.Amount.Should().Be(90);
    }

    [Fact]
    public async Task ApproveMilestoneAsync_ReleasesFundsToExpert()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 700, HeldBalance = 0, Currency = "AICOIN" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 90, TotalEarned = 90, Currency = "AICOIN" };
        var systemPlatformWallet = new Wallet { UserId = Aivora.Repositories.Constants.SystemConstants.SystemUserId, AvailableBalance = 0, Currency = "AICOIN" };

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE, Currency = "AICOIN" };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Amount = 300, Status = MilestoneStatus.SUBMITTED, Title = "Milestone 1", Currency = "AICOIN" };

        // Mock the initial deposit payment so treasury can find it
        var depositPayment = new Payment { MilestoneId = milestoneId, ProjectId = projectId, PayerId = clientId, PayeeId = expertId, Amount = 90, Status = PaymentStatus.RELEASED, Currency = "AICOIN" };

        dbContext.Wallets.AddRange(clientWallet, expertWallet, systemPlatformWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.Payments.Add(depositPayment);
        await dbContext.SaveChangesAsync();

        // Finance setup
        var commissionOptions = Options.Create(new CommissionOptions { Rate = 0.10m });
        var treasury = new Aivora.Services.Treasury.Treasury(dbContext, new Aivora.Services.Treasury.CommissionCalculator(commissionOptions), Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var service = new Service(dbContext, treasury, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());

        // Act
        var result = await service.ApproveMilestoneAsync(clientId, milestoneId);

        // Assert
        result.Status.Should().Be(MilestoneStatus.RELEASED);

        var updatedClientWallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == clientId);
        updatedClientWallet!.AvailableBalance.Should().Be(490); // 700 - 210

        var updatedExpertWallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == expertId);
        // Fee = 10% of 300 = 30. Remaining to expert = 210 - 30 = 180.
        updatedExpertWallet!.AvailableBalance.Should().Be(270); // 90 + 180
        updatedExpertWallet!.TotalEarned.Should().Be(270); // 90 + 180

        // In PayRemainingAsync, a new payment is created for the remaining 70% (fee is tracked via WalletTransaction)
        var payments = await dbContext.Payments.Where(p => p.MilestoneId == milestoneId).ToListAsync();
        payments.Count.Should().Be(2);
        payments.All(p => p.Status == PaymentStatus.RELEASED).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateMilestoneAsync_RelaxesDueDateConstraint_ForActiveMilestones()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        // Status is not CREATED (e.g. IN_PROGRESS)
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Amount = 300, Status = MilestoneStatus.IN_PROGRESS, Title = "Milestone 1", DueDate = new DateOnly(2026, 7, 7) };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());
        var request = new Request.UpdateMilestoneRequest
        {
            DueDate = new DateOnly(2026, 7, 10)
        };

        // Act
        var result = await service.UpdateMilestoneAsync(clientId, milestoneId, request);

        // Assert
        result.DueDate.Should().Be(new DateOnly(2026, 7, 10));
        var dbMilestone = await dbContext.Milestones.FindAsync(milestoneId);
        dbMilestone!.DueDate.Should().Be(new DateOnly(2026, 7, 10));
    }

    [Fact]
    public async Task UpdateMilestoneAsync_NonDueDateUpdateOnActiveMilestone_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Amount = 300, Status = MilestoneStatus.IN_PROGRESS, Title = "Milestone 1" };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());
        var request = new Request.UpdateMilestoneRequest
        {
            Title = "Updated Title"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateMilestoneAsync(clientId, milestoneId, request));
    }

    [Fact]
    public async Task CreateMilestoneAsync_WhenProjectDisputed_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = Guid.NewGuid(), Title = "Test Project", Status = ProjectStatus.DISPUTED };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());
        var request = new Request.CreateMilestoneRequest { Title = "M1", Amount = 100 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateMilestoneAsync(clientId, projectId, request));
        ex.Message.Should().Be("Cannot create a milestone while there is an active dispute.");
    }

    [Fact]
    public async Task UpdateMilestoneAsync_WhenMilestoneDisputed_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = Guid.NewGuid(), Title = "Test Project", Status = ProjectStatus.DISPUTED };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Amount = 300, Status = MilestoneStatus.DISPUTED, Title = "Milestone 1" };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());
        var request = new Request.UpdateMilestoneRequest { DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)) };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateMilestoneAsync(clientId, milestoneId, request));
        ex.Message.Should().Be("Cannot update a milestone while there is an active dispute.");
    }

    [Fact]
    public async Task UpdateMilestoneAsync_WhenProjectDisputed_ThrowsValidationException()
    {
        // Arrange: milestone itself is not DISPUTED, but a sibling milestone made the project DISPUTED
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = Guid.NewGuid(), Title = "Test Project", Status = ProjectStatus.DISPUTED };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Amount = 300, Status = MilestoneStatus.CREATED, Title = "Milestone 1" };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());
        var request = new Request.UpdateMilestoneRequest { DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)) };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateMilestoneAsync(clientId, milestoneId, request));
        ex.Message.Should().Be("Cannot update a milestone while there is an active dispute.");
    }

    [Fact]
    public async Task CreateMilestoneAsync_TitleTooLong_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = Guid.NewGuid(), Title = "Test Project", Status = ProjectStatus.ACTIVE };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());
        var request = new Request.CreateMilestoneRequest { Title = new string('a', 256), Amount = 100 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateMilestoneAsync(clientId, projectId, request));
        ex.Message.Should().Be("Title must not exceed 255 characters.");
    }

    [Fact]
    public async Task UpdateMilestoneAsync_TitleTooLong_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Amount = 300, Status = MilestoneStatus.CREATED, Title = "Milestone 1" };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());
        var request = new Request.UpdateMilestoneRequest { Title = new string('a', 256) };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateMilestoneAsync(clientId, milestoneId, request));
        ex.Message.Should().Be("Title must not exceed 255 characters.");
    }

    [Fact]
    public async Task CreateMilestoneAsync_DescriptionTooLong_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = Guid.NewGuid(), Title = "Test Project", Status = ProjectStatus.ACTIVE };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());
        var request = new Request.CreateMilestoneRequest { Title = "Valid title", Description = new string('a', 1001), Amount = 100 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateMilestoneAsync(clientId, projectId, request));
        ex.Message.Should().Be("Description must not exceed 1000 characters.");
    }

    [Fact]
    public async Task CreateMilestoneAsync_AcceptanceCriteriaTooLong_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = Guid.NewGuid(), Title = "Test Project", Status = ProjectStatus.ACTIVE };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());
        var request = new Request.CreateMilestoneRequest { Title = "Valid title", AcceptanceCriteria = new string('a', 2001), Amount = 100 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateMilestoneAsync(clientId, projectId, request));
        ex.Message.Should().Be("AcceptanceCriteria must not exceed 2000 characters.");
    }

    [Fact]
    public async Task CreateMilestoneAsync_WithValidRequest_ReturnsMilestone()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = Guid.NewGuid(), Title = "Test Project", Status = ProjectStatus.ACTIVE, TotalBudget = 1000 };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());
        var request = new Request.CreateMilestoneRequest { Title = "Milestone 1", Amount = 300 };

        // Act
        var result = await service.CreateMilestoneAsync(clientId, projectId, request);

        // Assert
        result.Title.Should().Be("Milestone 1");
        result.Amount.Should().Be(300);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateMilestoneAsync_AmountNotPositive_ThrowsValidationException(decimal amount)
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = Guid.NewGuid(), Title = "Test Project", Status = ProjectStatus.ACTIVE };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());
        var request = new Request.CreateMilestoneRequest { Title = "Milestone 1", Amount = amount };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateMilestoneAsync(clientId, projectId, request));
        ex.Message.Should().Be("Amount must be greater than 0.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateMilestoneAsync_EmptyTitle_ThrowsValidationException(string title)
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = Guid.NewGuid(), Title = "Test Project", Status = ProjectStatus.ACTIVE };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());
        var request = new Request.CreateMilestoneRequest { Title = title, Amount = 100 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateMilestoneAsync(clientId, projectId, request));
        ex.Message.Should().Be("Title is required.");
    }

    [Fact]
    public async Task CreateMilestoneAsync_ExceedsProjectBudget_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = Guid.NewGuid(), Title = "Test Project", Status = ProjectStatus.ACTIVE, TotalBudget = 500 };
        var existingMilestone = new Milestone { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Milestone 1", Amount = 300, Status = MilestoneStatus.CREATED };
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(existingMilestone);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());
        var request = new Request.CreateMilestoneRequest { Title = "Milestone 2", Amount = 300 };

        // Act & Assert (300 existing + 300 new = 600 > 500 budget)
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateMilestoneAsync(clientId, projectId, request));
        ex.Message.Should().Be("Total milestone amount exceeds the project's total budget.");
    }

    [Fact]
    public async Task CreateMilestoneAsync_WithinProjectBudget_Success()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = Guid.NewGuid(), Title = "Test Project", Status = ProjectStatus.ACTIVE, TotalBudget = 500 };
        var existingMilestone = new Milestone { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Milestone 1", Amount = 200, Status = MilestoneStatus.CREATED };
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(existingMilestone);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());
        var request = new Request.CreateMilestoneRequest { Title = "Milestone 2", Amount = 300 };

        // Act (200 existing + 300 new = 500, exactly at budget)
        var result = await service.CreateMilestoneAsync(clientId, projectId, request);

        // Assert
        result.Amount.Should().Be(300);
    }

    [Fact]
    public async Task CreateMilestoneAsync_NoProjectBudget_SkipsBudgetCheck()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = Guid.NewGuid(), Title = "Test Project", Status = ProjectStatus.ACTIVE, TotalBudget = null };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());
        var request = new Request.CreateMilestoneRequest { Title = "Milestone 1", Amount = 999999 };

        // Act
        var result = await service.CreateMilestoneAsync(clientId, projectId, request);

        // Assert
        result.Amount.Should().Be(999999);
    }
}

