using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.DisputeService;
using Aivora.Services.FinancialLedger;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class DisputeServiceTests
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
    public async Task OpenDisputeAsync_FreesPaymentsAndUpdatesStatuses()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        var clientUser = new User { Id = clientId, FullName = "Client", Role = UserRole.CLIENT, Email = "c@t.com", PasswordHash = "x" };
        var expertUser = new User { Id = expertId, FullName = "Expert", Role = UserRole.EXPERT, Email = "e@t.com", PasswordHash = "x" };
        var project = new Project { Id = projectId, ClientId = clientId, ExpertId = expertId, Title = "Dispute Project", Status = ProjectStatus.ACTIVE };
        var milestone = new Milestone { Id = milestoneId, ProjectId = projectId, Amount = 500, Status = MilestoneStatus.FUNDED, Title = "M1" };
        var payment = new Payment { MilestoneId = milestoneId, ProjectId = projectId, PayerId = clientId, PayeeId = expertId, Amount = 500, Status = PaymentStatus.HELD };

        dbContext.Users.AddRange(clientUser, expertUser);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var ledger = new FinancialLedger(dbContext);
        var service = new Service(dbContext, ledger);
        var request = new Request.OpenDisputeRequest { MilestoneId = milestoneId, Reason = "Poor quality" };

        // Act
        var result = await service.OpenDisputeAsync(clientId, request);

        // Assert
        result.Status.Should().Be(DisputeStatus.OPEN.ToString());
        
        var updatedMilestone = await dbContext.Milestones.FindAsync(milestoneId);
        updatedMilestone!.Status.Should().Be(MilestoneStatus.DISPUTED);

        var updatedProject = await dbContext.Projects.FindAsync(projectId);
        updatedProject!.Status.Should().Be(ProjectStatus.DISPUTED);

        var updatedPayment = await dbContext.Payments.FindAsync(payment.Id);
        updatedPayment!.Status.Should().Be(PaymentStatus.FROZEN);
    }

    [Fact]
    public async Task ResolveDisputeAsync_RefundToClient_UpdatesWalletsCorrectly()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        
        var clientUser = new User { Id = clientId, FullName = "Client", Role = UserRole.CLIENT, Email = "c@t.com", PasswordHash = "x" };
        var expertUser = new User { Id = expertId, FullName = "Expert", Role = UserRole.EXPERT, Email = "e@t.com", PasswordHash = "x" };
        var adminUser = new User { Id = adminId, FullName = "Admin", Role = UserRole.ADMIN, Email = "a@t.com", PasswordHash = "x" };

        var clientWallet = new Wallet { UserId = clientId, AvailableBalance = 0, HeldBalance = 500, Currency = "AICOIN" };
        var expertWallet = new Wallet { UserId = expertId, AvailableBalance = 0, Currency = "AICOIN" };
        
        var project = new Project { Id = Guid.NewGuid(), ClientId = clientId, ExpertId = expertId, Title = "Resolve Project", Status = ProjectStatus.DISPUTED };
        var milestone = new Milestone { Id = Guid.NewGuid(), ProjectId = project.Id, Amount = 500, Status = MilestoneStatus.DISPUTED, Title = "M1" };
        var payment = new Payment { Id = Guid.NewGuid(), MilestoneId = milestone.Id, ProjectId = project.Id, PayerId = clientId, PayeeId = expertId, Amount = 500, Status = PaymentStatus.FROZEN };
        
        var dispute = new Dispute { Id = Guid.NewGuid(), ProjectId = project.Id, MilestoneId = milestone.Id, PaymentId = payment.Id, OpenedBy = clientId, AgainstUserId = expertId, Status = DisputeStatus.OPEN, Reason = "X" };

        dbContext.Users.AddRange(clientUser, expertUser, adminUser);
        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        dbContext.Payments.Add(payment);
        dbContext.Disputes.Add(dispute);
        await dbContext.SaveChangesAsync();

        var ledger = new FinancialLedger(dbContext);
        var service = new Service(dbContext, ledger);
        var resolveRequest = new Request.ResolveDisputeRequest 
        { 
            ResolutionType = DisputeResolutionType.REFUND_TO_CLIENT,
            ResolutionNote = "Validated refund" 
        };

        // Act
        await service.ResolveDisputeAsync(adminId, dispute.Id, resolveRequest);

        // Assert
        var updatedClientWallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == clientId);
        updatedClientWallet!.HeldBalance.Should().Be(0);
        updatedClientWallet!.AvailableBalance.Should().Be(500);

        var updatedPayment = await dbContext.Payments.FindAsync(payment.Id);
        updatedPayment!.Status.Should().Be(PaymentStatus.REFUNDED);

        var updatedDispute = await dbContext.Disputes.FindAsync(dispute.Id);
        updatedDispute!.Status.Should().Be(DisputeStatus.RESOLVED);
    }
}
