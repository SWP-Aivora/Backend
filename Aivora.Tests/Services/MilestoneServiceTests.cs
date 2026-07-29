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
        var treasury = new Aivora.Services.Treasury.Treasury(dbContext, new Aivora.Services.Treasury.CommissionCalculator(commissionOptions), Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService(), Options.Create(new Aivora.Services.Options.EscrowOptions()));
        var service = new Service(dbContext, treasury, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());

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
        var treasury = new Aivora.Services.Treasury.Treasury(dbContext, new Aivora.Services.Treasury.CommissionCalculator(commissionOptions), Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService(), Options.Create(new Aivora.Services.Options.EscrowOptions()));
        var service = new Service(dbContext, treasury, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());

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
    public async Task GetMilestoneByIdAsync_DerivesDueDaysFromDueDateAndCreatedAt()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Amount = 300, Status = MilestoneStatus.CREATED, Title = "Milestone 1", DueDate = today.AddDays(10) };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());

        // Act
        var result = await service.GetMilestoneByIdAsync(clientId, milestoneId);

        // Assert
        result.DueDays.Should().Be(10);
    }

    [Fact]
    public async Task GetMilestoneByIdAsync_DueDateNull_DueDaysNull()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Amount = 300, Status = MilestoneStatus.CREATED, Title = "Milestone 1", DueDate = null };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());

        // Act
        var result = await service.GetMilestoneByIdAsync(clientId, milestoneId);

        // Assert
        result.DueDays.Should().BeNull();
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

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
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

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
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

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
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

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
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

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
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

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
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

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var request = new Request.UpdateMilestoneRequest { Title = new string('a', 256) };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateMilestoneAsync(clientId, milestoneId, request));
        ex.Message.Should().Be("Title must not exceed 255 characters.");
    }

    [Fact]
    public async Task UpdateMilestoneAsync_ExceedsProjectBudget_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE, TotalBudget = 500 };
        var otherMilestone = new Milestone { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Milestone 1", Amount = 200, Status = MilestoneStatus.CREATED };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 2", Amount = 100, Status = MilestoneStatus.CREATED };

        dbContext.Projects.Add(project);
        dbContext.Milestones.AddRange(otherMilestone, milestone);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var request = new Request.UpdateMilestoneRequest { Amount = 400 };

        // Act & Assert (200 other + 400 new = 600 > 500 budget)
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateMilestoneAsync(clientId, milestoneId, request));
        ex.Message.Should().Be("Total milestone amount exceeds the project's total budget.");
    }

    [Fact]
    public async Task UpdateMilestoneAsync_WithinProjectBudget_Success()
    {
        // Arrange: milestone's own old amount (300) must be excluded from the sum, otherwise
        // this would false-fail as a double-count (300 old + 300 new + 200 other = 800 > 500).
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE, TotalBudget = 500 };
        var otherMilestone = new Milestone { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Milestone 1", Amount = 200, Status = MilestoneStatus.CREATED };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Milestone 2", Amount = 300, Status = MilestoneStatus.CREATED };

        dbContext.Projects.Add(project);
        dbContext.Milestones.AddRange(otherMilestone, milestone);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var request = new Request.UpdateMilestoneRequest { Amount = 300 };

        // Act (200 other + 300 new = 500, exactly at budget)
        var result = await service.UpdateMilestoneAsync(clientId, milestoneId, request);

        // Assert
        result.Amount.Should().Be(300);
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

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
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

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
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

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
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

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
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

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
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

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
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

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var request = new Request.CreateMilestoneRequest { Title = "Milestone 2", Amount = 300 };

        // Act (200 existing + 300 new = 500, exactly at budget)
        var result = await service.CreateMilestoneAsync(clientId, projectId, request);

        // Assert
        result.Amount.Should().Be(300);
    }

    [Fact]
    public async Task UpdateMilestoneAsync_ShiftsDueDate_CascadesToLaterMilestonesAndNonTerminalSteps()
    {
        // Arrange: 3 milestones (OrderIndex 1,2,3), each with a non-terminal step and a terminal step.
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var m1Id = Guid.NewGuid();
        var m2Id = Guid.NewGuid();
        var m3Id = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };

        var m1 = new Milestone { Id = m1Id, ProjectId = projectId, Title = "M1", Amount = 100, OrderIndex = 1, Status = MilestoneStatus.IN_PROGRESS, DueDate = new DateOnly(2026, 8, 1) };
        var m2 = new Milestone { Id = m2Id, ProjectId = projectId, Title = "M2", Amount = 100, OrderIndex = 2, Status = MilestoneStatus.CREATED, DueDate = new DateOnly(2026, 8, 10) };
        var m3 = new Milestone { Id = m3Id, ProjectId = projectId, Title = "M3", Amount = 100, OrderIndex = 3, Status = MilestoneStatus.CREATED, DueDate = new DateOnly(2026, 8, 20) };

        dbContext.Projects.Add(project);
        dbContext.Milestones.AddRange(m1, m2, m3);

        var m1ActiveStep = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = m1Id, Title = "M1 active step", OrderIndex = 1, Status = MilestoneStepStatus.IN_PROGRESS, DueDate = new DateOnly(2026, 7, 30) };
        var m1DoneStep = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = m1Id, Title = "M1 done step", OrderIndex = 2, Status = MilestoneStepStatus.COMPLETED, DueDate = new DateOnly(2026, 7, 29) };
        var m2ActiveStep = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = m2Id, Title = "M2 active step", OrderIndex = 1, Status = MilestoneStepStatus.PENDING, DueDate = new DateOnly(2026, 8, 9) };
        var m2SkippedStep = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = m2Id, Title = "M2 skipped step", OrderIndex = 2, Status = MilestoneStepStatus.SKIPPED, DueDate = new DateOnly(2026, 8, 8) };
        var m3ActiveStep = new MilestoneStep { Id = Guid.NewGuid(), MilestoneId = m3Id, Title = "M3 active step", OrderIndex = 1, Status = MilestoneStepStatus.PENDING, DueDate = new DateOnly(2026, 8, 19) };

        dbContext.MilestoneSteps.AddRange(m1ActiveStep, m1DoneStep, m2ActiveStep, m2SkippedStep, m3ActiveStep);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());

        // Act: shift M1's DueDate by +5 days (2026-08-01 -> 2026-08-06)
        var result = await service.UpdateMilestoneAsync(clientId, m1Id, new Request.UpdateMilestoneRequest { DueDate = new DateOnly(2026, 8, 6) });

        // Assert: cascaded count = 2 later milestones (M2, M3)
        result.CascadedMilestoneCount.Should().Be(2);

        var dbM2 = await dbContext.Milestones.FindAsync(m2Id);
        var dbM3 = await dbContext.Milestones.FindAsync(m3Id);
        dbM2!.DueDate.Should().Be(new DateOnly(2026, 8, 15));
        dbM3!.DueDate.Should().Be(new DateOnly(2026, 8, 25));

        // M1's own non-terminal step shifts, terminal step does not
        (await dbContext.MilestoneSteps.FindAsync(m1ActiveStep.Id))!.DueDate.Should().Be(new DateOnly(2026, 8, 4));
        (await dbContext.MilestoneSteps.FindAsync(m1DoneStep.Id))!.DueDate.Should().Be(new DateOnly(2026, 7, 29));

        // M2/M3 non-terminal steps shift, terminal (SKIPPED) does not
        (await dbContext.MilestoneSteps.FindAsync(m2ActiveStep.Id))!.DueDate.Should().Be(new DateOnly(2026, 8, 14));
        (await dbContext.MilestoneSteps.FindAsync(m2SkippedStep.Id))!.DueDate.Should().Be(new DateOnly(2026, 8, 8));
        (await dbContext.MilestoneSteps.FindAsync(m3ActiveStep.Id))!.DueDate.Should().Be(new DateOnly(2026, 8, 24));
    }

    [Fact]
    public async Task UpdateMilestoneAsync_ShiftsLastMilestoneDueDate_CascadesNothing()
    {
        // Arrange: only one milestone (the last one) — no later milestones to cascade to.
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "Only Milestone", Amount = 100, OrderIndex = 1, Status = MilestoneStatus.CREATED, DueDate = new DateOnly(2026, 8, 1) };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());

        // Act
        var result = await service.UpdateMilestoneAsync(clientId, milestoneId, new Request.UpdateMilestoneRequest { DueDate = new DateOnly(2026, 8, 15) });

        // Assert
        result.CascadedMilestoneCount.Should().Be(0);
        result.DueDate.Should().Be(new DateOnly(2026, 8, 15));
    }

    [Fact]
    public async Task UpdateMilestoneAsync_SettledLaterMilestone_DoesNotCascade()
    {
        // Arrange: a later milestone that is already RELEASED must not move.
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var m1Id = Guid.NewGuid();
        var m2Id = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var m1 = new Milestone { Id = m1Id, ProjectId = projectId, Title = "M1", Amount = 100, OrderIndex = 1, Status = MilestoneStatus.IN_PROGRESS, DueDate = new DateOnly(2026, 8, 1) };
        var m2 = new Milestone { Id = m2Id, ProjectId = projectId, Title = "M2", Amount = 100, OrderIndex = 2, Status = MilestoneStatus.RELEASED, DueDate = new DateOnly(2026, 8, 10) };

        dbContext.Projects.Add(project);
        dbContext.Milestones.AddRange(m1, m2);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());

        // Act
        var result = await service.UpdateMilestoneAsync(clientId, m1Id, new Request.UpdateMilestoneRequest { DueDate = new DateOnly(2026, 8, 6) });

        // Assert: settled milestone is excluded from cascade
        result.CascadedMilestoneCount.Should().Be(0);
        (await dbContext.Milestones.FindAsync(m2Id))!.DueDate.Should().Be(new DateOnly(2026, 8, 10));
    }

    [Fact]
    public async Task AddMilestoneStepAsync_StepDueDateAfterMilestoneDueDate_ThrowsValidationException()
    {
        // Arrange: milestone due day 30, step requested at day 35 (after) -> rejected
        var dbContext = GetDbContext();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var milestoneDueDate = new DateOnly(2026, 8, 30);

        var project = new Project { Id = projectId, ClientId = Guid.NewGuid(), ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "M1", Amount = 100, Status = MilestoneStatus.IN_PROGRESS, DueDate = milestoneDueDate };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var request = new Request.CreateMilestoneStepRequest { Title = "Late step", DueDate = milestoneDueDate.AddDays(5), OrderIndex = 1 };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.AddMilestoneStepAsync(expertId, milestoneId, request));
        ex.Message.Should().Be("Step due date cannot be after the milestone's due date.");
    }

    [Fact]
    public async Task AddMilestoneStepAsync_StepDueDateBeforeMilestoneDueDate_Succeeds()
    {
        // Arrange: milestone due day 30, step at day 25 (before) -> accepted
        var dbContext = GetDbContext();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var milestoneDueDate = new DateOnly(2026, 8, 30);

        var project = new Project { Id = projectId, ClientId = Guid.NewGuid(), ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "M1", Amount = 100, Status = MilestoneStatus.IN_PROGRESS, DueDate = milestoneDueDate };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var request = new Request.CreateMilestoneStepRequest { Title = "Early step", DueDate = milestoneDueDate.AddDays(-5), OrderIndex = 1 };

        // Act
        var result = await service.AddMilestoneStepAsync(expertId, milestoneId, request);

        // Assert
        result.DueDate.Should().Be(milestoneDueDate.AddDays(-5));
    }

    [Fact]
    public async Task AddMilestoneStepAsync_StepDueDateEqualsMilestoneDueDate_Succeeds()
    {
        // Arrange: boundary case — step DueDate == milestone DueDate must be ACCEPTED (check is >, not >=)
        var dbContext = GetDbContext();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var milestoneDueDate = new DateOnly(2026, 8, 30);

        var project = new Project { Id = projectId, ClientId = Guid.NewGuid(), ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "M1", Amount = 100, Status = MilestoneStatus.IN_PROGRESS, DueDate = milestoneDueDate };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var request = new Request.CreateMilestoneStepRequest { Title = "Boundary step", DueDate = milestoneDueDate, OrderIndex = 1 };

        // Act
        var result = await service.AddMilestoneStepAsync(expertId, milestoneId, request);

        // Assert
        result.DueDate.Should().Be(milestoneDueDate);
    }

    [Fact]
    public async Task AddMilestoneStepAsync_MilestoneDueDateNull_SkipsValidationRegardlessOfStepDate()
    {
        // Arrange: pre-#196 project whose milestone never got a computed DueDate.
        var dbContext = GetDbContext();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = Guid.NewGuid(), ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "M1", Amount = 100, Status = MilestoneStatus.IN_PROGRESS, DueDate = null };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var request = new Request.CreateMilestoneStepRequest { Title = "Far future step", DueDate = new DateOnly(2099, 1, 1), OrderIndex = 1 };

        // Act: should not throw even though step date is far in the future, because milestone.DueDate is null
        var result = await service.AddMilestoneStepAsync(expertId, milestoneId, request);

        // Assert
        result.DueDate.Should().Be(new DateOnly(2099, 1, 1));
    }

    [Fact]
    public async Task UpdateMilestoneStepAsync_StepDueDateAfterMilestoneDueDate_ThrowsValidationException()
    {
        // Arrange
        var dbContext = GetDbContext();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var milestoneDueDate = new DateOnly(2026, 8, 30);

        var project = new Project { Id = projectId, ClientId = Guid.NewGuid(), ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "M1", Amount = 100, Status = MilestoneStatus.IN_PROGRESS, DueDate = milestoneDueDate };
        var step = new MilestoneStep { Id = stepId, MilestoneId = milestoneId, Title = "Custom step", OrderIndex = 1, Status = MilestoneStepStatus.PENDING };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.Add(step);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var request = new Request.UpdateMilestoneStepRequest { DueDate = milestoneDueDate.AddDays(5) };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateMilestoneStepAsync(expertId, stepId, request));
        ex.Message.Should().Be("Step due date cannot be after the milestone's due date.");
    }

    [Fact]
    public async Task UpdateMilestoneStepAsync_MilestoneDueDateNull_SkipsValidationRegardlessOfStepDate()
    {
        // Arrange
        var dbContext = GetDbContext();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = Guid.NewGuid(), ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "M1", Amount = 100, Status = MilestoneStatus.IN_PROGRESS, DueDate = null };
        var step = new MilestoneStep { Id = stepId, MilestoneId = milestoneId, Title = "Custom step", OrderIndex = 1, Status = MilestoneStepStatus.PENDING };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.MilestoneSteps.Add(step);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var request = new Request.UpdateMilestoneStepRequest { DueDate = new DateOnly(2099, 1, 1) };

        // Act: should not throw even though step date is far in the future, because milestone.DueDate is null
        var result = await service.UpdateMilestoneStepAsync(expertId, stepId, request);

        // Assert
        result.DueDate.Should().Be(new DateOnly(2099, 1, 1));
    }

    [Fact]
    public async Task SuggestMilestoneStepsAsync_MapsEstimatedDaysThrough()
    {
        // Arrange
        var dbContext = GetDbContext();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var project = new Project { Id = projectId, ClientId = Guid.NewGuid(), ExpertId = expertId, Title = "Test Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Title = "M1", Amount = 100, Status = MilestoneStatus.CREATED };

        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var draft = new Aivora.Services.AIMilestoneStepAssistantService.AIMilestoneStepSuggestionDraft
        {
            Steps = new List<Aivora.Services.AIMilestoneStepAssistantService.Response.SuggestedStep>
            {
                new() { Title = "Step 1", Description = "Desc 1", EstimatedDays = 3 }
            },
            AIModel = "Test-Model"
        };

        var providerMock = new Mock<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>();
        providerMock
            .Setup(p => p.GenerateSuggestionAsync(It.IsAny<Aivora.Services.AIMilestoneStepAssistantService.Request.SuggestMilestoneStepsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), providerMock.Object, new Aivora.Services.RealtimeService.NullRealtimeService());

        // Act
        var result = await service.SuggestMilestoneStepsAsync(expertId, milestoneId);

        // Assert
        result.Steps.Should().ContainSingle(s => s.Title == "Step 1" && s.EstimatedDays == 3);
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

        var service = new Service(dbContext, Mock.Of<ITreasury>(), Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var request = new Request.CreateMilestoneRequest { Title = "Milestone 1", Amount = 999999 };

        // Act
        var result = await service.CreateMilestoneAsync(clientId, projectId, request);

        // Assert
        result.Amount.Should().Be(999999);
    }
}

