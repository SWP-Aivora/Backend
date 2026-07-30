using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Aivora.Services.ProjectService;
using Aivora.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;

namespace Aivora.Tests.Unit.ProjectServiceTests;

public class ServiceTests : IDisposable
{
    private readonly AivoraDbContext _dbContext;
    private readonly IService _service;

    public ServiceTests()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new AivoraDbContext(options);

        _service = new Service(_dbContext, new Aivora.Services.RealtimeService.NullRealtimeService());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    /// <summary>
    /// Helper: tạo Project kèm User entities cần thiết cho Include() navigation properties.
    /// </summary>
    private async Task SeedProjectAsync(Guid clientId, Guid expertId, Guid projectId)
    {
        // Tạo User entities để Include() không bị lỗi với InMemory provider
        var client = new User
        {
            Id = clientId,
            Email = $"client-{clientId}@test.com",
            FullName = "Test Client",
            Role = UserRole.CLIENT,
            PasswordHash = "hash"
        };
        var expert = new User
        {
            Id = expertId,
            Email = $"expert-{expertId}@test.com",
            FullName = "Test Expert",
            Role = UserRole.EXPERT,
            PasswordHash = "hash"
        };

        _dbContext.Users.Add(client);
        _dbContext.Users.Add(expert);

        var project = new Project
        {
            Id = projectId,
            ClientId = clientId,
            ExpertId = expertId,
            Title = "Test Project",
            Status = ProjectStatus.ACTIVE
        };
        _dbContext.Projects.Add(project);

        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task GetProjectByIdAsync_AdminUser_ShouldReturnProject()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(clientId, expertId, projectId);
        var adminUserId = Guid.NewGuid();

        // Act
        var result = await _service.GetProjectByIdAsync(adminUserId, projectId, UserRole.ADMIN);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(projectId);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ClientUser_OwnProject_ShouldReturnProject()
    {
        // Arrange
        var clientUserId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(clientUserId, expertId, projectId);

        // Act
        var result = await _service.GetProjectByIdAsync(clientUserId, projectId, UserRole.CLIENT);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(projectId);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ExpertUser_OwnProject_ShouldReturnProject()
    {
        // Arrange
        var clientUserId = Guid.NewGuid();
        var expertUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(clientUserId, expertUserId, projectId);

        // Act
        var result = await _service.GetProjectByIdAsync(expertUserId, projectId, UserRole.EXPERT);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(projectId);
    }

    [Fact]
    public async Task GetProjectByIdAsync_ClientUser_OtherProject_ShouldThrowForbidden()
    {
        // Arrange
        var ownerClientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(ownerClientId, expertId, projectId);

        var otherClientId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.GetProjectByIdAsync(otherClientId, projectId, UserRole.CLIENT)
        );
    }

    [Fact]
    public async Task GetProjectByIdAsync_ExpertUser_UnassignedProject_ShouldThrowForbidden()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var assignedExpertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(clientId, assignedExpertId, projectId);

        var differentExpertId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.GetProjectByIdAsync(differentExpertId, projectId, UserRole.EXPERT)
        );
    }

    [Fact]
    public async Task GetProjectByIdAsync_AdminUser_OwnProject_ShouldReturnProject()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(clientId, expertId, projectId);
        var adminUserId = Guid.NewGuid();

        // Act
        var result = await _service.GetProjectByIdAsync(adminUserId, projectId, UserRole.ADMIN);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(projectId);
    }

    [Fact]
    public async Task GetProjectByIdAsync_MilestoneHasDueDate_DerivesDueDaysFromDueDateAndCreatedAt()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(clientId, expertId, projectId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _dbContext.Milestones.Add(new Milestone
        {
            ProjectId = projectId,
            Title = "Milestone 1",
            Amount = 300,
            Status = MilestoneStatus.CREATED,
            DueDate = today.AddDays(10)
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetProjectByIdAsync(clientId, projectId, UserRole.CLIENT);

        // Assert
        result.Milestones.Single().DueDays.Should().Be(10);
    }

    [Fact]
    public async Task GetProjectByIdAsync_MilestoneDueDateNull_DueDaysNull()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await SeedProjectAsync(clientId, expertId, projectId);

        _dbContext.Milestones.Add(new Milestone
        {
            ProjectId = projectId,
            Title = "Milestone 1",
            Amount = 300,
            Status = MilestoneStatus.CREATED,
            DueDate = null
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetProjectByIdAsync(clientId, projectId, UserRole.CLIENT);

        // Assert
        result.Milestones.Single().DueDays.Should().BeNull();
    }

    // ==================== CancelDisputedProjectAsync Tests (#202) ====================

    private async Task<(Project project, Dispute dispute)> SeedDisputedProjectAsync(Guid clientId, Guid expertId, DisputeStatus disputeStatus = DisputeStatus.OPEN)
    {
        var client = new User { Id = clientId, Email = $"client-{clientId}@test.com", FullName = "Test Client", Role = UserRole.CLIENT, PasswordHash = "hash" };
        var expert = new User { Id = expertId, Email = $"expert-{expertId}@test.com", FullName = "Test Expert", Role = UserRole.EXPERT, PasswordHash = "hash" };
        _dbContext.Users.AddRange(client, expert);

        var project = new Project { ClientId = clientId, ExpertId = expertId, Title = "Disputed Project", Status = ProjectStatus.DISPUTED };
        _dbContext.Projects.Add(project);

        var milestone = new Milestone { Project = project, Title = "M1", Amount = 500, Status = MilestoneStatus.DISPUTED };
        _dbContext.Milestones.Add(milestone);

        var payment = new Payment { MilestoneId = milestone.Id, ProjectId = project.Id, PayerId = clientId, PayeeId = expertId, Amount = 500, Status = PaymentStatus.RELEASED };
        _dbContext.Payments.Add(payment);

        var dispute = new Dispute
        {
            ProjectId = project.Id,
            MilestoneId = milestone.Id,
            PaymentId = payment.Id,
            OpenedBy = clientId,
            AgainstUserId = expertId,
            Reason = "Quality issue",
            Status = disputeStatus
        };
        _dbContext.Disputes.Add(dispute);

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 1000, Currency = "AICOIN" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 500, Currency = "AICOIN" };
        _dbContext.Wallets.AddRange(clientWallet, expertWallet);

        await _dbContext.SaveChangesAsync();
        return (project, dispute);
    }

    [Fact]
    public async Task CancelDisputedProjectAsync_ByClient_ShouldCancelProjectAndCloseDispute()
    {
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var (project, dispute) = await SeedDisputedProjectAsync(clientId, expertId);

        var result = await _service.CancelDisputedProjectAsync(clientId, project.Id);

        result.Should().NotBeNull();
        var dbProject = await _dbContext.Projects.FindAsync(project.Id);
        dbProject!.Status.Should().Be(ProjectStatus.CANCELLED);
        dbProject.CancelledAt.Should().NotBeNull();

        var dbDispute = await _dbContext.Disputes.FindAsync(dispute.Id);
        dbDispute!.Status.Should().Be(DisputeStatus.CLOSED);

        // No wallet/payment mutation
        var clientWallet = await _dbContext.Wallets.FirstAsync(w => w.UserId == clientId);
        var expertWallet = await _dbContext.Wallets.FirstAsync(w => w.UserId == expertId);
        clientWallet.AvailableBalance.Should().Be(1000);
        expertWallet.AvailableBalance.Should().Be(500);
        var dbPayment = await _dbContext.Payments.FirstAsync(p => p.ProjectId == project.Id);
        dbPayment.Status.Should().Be(PaymentStatus.RELEASED);
    }

    [Fact]
    public async Task CancelDisputedProjectAsync_ByExpert_ShouldCancelProjectAndCloseDispute()
    {
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var (project, dispute) = await SeedDisputedProjectAsync(clientId, expertId);

        var result = await _service.CancelDisputedProjectAsync(expertId, project.Id);

        result.Should().NotBeNull();
        var dbProject = await _dbContext.Projects.FindAsync(project.Id);
        dbProject!.Status.Should().Be(ProjectStatus.CANCELLED);

        var dbDispute = await _dbContext.Disputes.FindAsync(dispute.Id);
        dbDispute!.Status.Should().Be(DisputeStatus.CLOSED);
    }

    [Fact]
    public async Task CancelDisputedProjectAsync_ByThirdParty_ShouldThrowForbidden()
    {
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var (project, _) = await SeedDisputedProjectAsync(clientId, expertId);
        var thirdPartyId = Guid.NewGuid();

        var act = () => _service.CancelDisputedProjectAsync(thirdPartyId, project.Id);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CancelDisputedProjectAsync_NoActiveDispute_ShouldThrowValidation()
    {
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        // Dispute already CLOSED -> no active dispute left
        var (project, _) = await SeedDisputedProjectAsync(clientId, expertId, DisputeStatus.CLOSED);

        var act = () => _service.CancelDisputedProjectAsync(clientId, project.Id);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Message.Should().Contain("No active dispute");

        var dbProject = await _dbContext.Projects.FindAsync(project.Id);
        dbProject!.Status.Should().Be(ProjectStatus.DISPUTED);
    }
}
