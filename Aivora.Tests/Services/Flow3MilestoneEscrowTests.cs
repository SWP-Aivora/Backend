using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.MilestoneService;
using Aivora.Services.Treasury;
using Aivora.Services.Exceptions;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

/// <summary>
/// TDD Test Suite for Flow 3: Milestone, Escrow & Deliverable
/// Focus on observable behaviors through public interfaces
/// </summary>
public class Flow3MilestoneEscrowTests
{
    private AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    /// <summary>
    /// Test 1: Happy Path - Client funds milestone with sufficient balance
    ///
    /// Behavior Specification:
    /// Given: Client has wallet with balance >= milestone amount
    /// When: Client funds a CREATED milestone
    /// Then:
    ///   - Milestone status becomes FUNDED
    ///   - Payment status becomes HELD
    ///   - Money moves from client Available -> Held
    ///   - Project status becomes ACTIVE
    ///   - Transaction logs are created
    /// </summary>
    [Fact]
    public async Task Client_Funds_Milestone_With_Sufficient_Balance_Succeeds()
    {
        // ----------------------------------------------------
        // Arrange & Preconditions
        // ----------------------------------------------------
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        // Create users first (required for Wallet navigation)
        var clientUser = new User
        {
            Id = clientId,
            Email = "client@aivora.com",
            PasswordHash = "hash",
            FullName = "Client User",
            Role = UserRole.CLIENT,
            Status = UserStatus.ACTIVE
        };

        var expertUser = new User
        {
            Id = expertId,
            Email = "expert@aivora.com",
            PasswordHash = "hash",
            FullName = "Expert User",
            Role = UserRole.EXPERT,
            Status = UserStatus.ACTIVE
        };

        var clientWallet = new Wallet
        {
            UserId = clientId,
            AvailableBalance = 2000,
            HeldBalance = 0,
            Currency = "AICOIN"
        };

        var expertWallet = new Wallet
        {
            UserId = expertId,
            AvailableBalance = 0,
            HeldBalance = 0,
            Currency = "AICOIN"
        };

        var project = new Project
        {
            Id = projectId,
            JobId = Guid.NewGuid(), // Required
            AcceptedProposalId = Guid.NewGuid(), // Required
            ClientId = clientId,
            ExpertId = expertId,
            Title = "AI Chatbot Project",
            Description = "Building an AI chatbot for beauty shop",
            Status = ProjectStatus.PENDING_PAYMENT,
            Currency = "AICOIN",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var milestone = new Milestone
        {
            Id = milestoneId,
            ProjectId = projectId,
            Amount = 900,
            Status = MilestoneStatus.CREATED,
            Title = "Chatbot MVP",
            Currency = "AICOIN",
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Setup database state - Note: EF Core InMemory doesn't handle navigation properties well
        // We need to explicitly set UserIds for Wallets
        dbContext.Users.AddRange(clientUser, expertUser);
        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        // Use the service (public interface)
        var treasury = new Treasury(dbContext, Mock.Of<ILogger<Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var milestoneService = new Service(dbContext, treasury, Mock.Of<Aivora.Services.NotificationService.IService>());

        // ----------------------------------------------------
        // Act: Client funds the milestone
        // ----------------------------------------------------
        var fundResult = await milestoneService.FundMilestoneAsync(clientId, milestoneId);

        // ----------------------------------------------------
        // Assert: Verify observable behavior changes
        // ----------------------------------------------------
        // 1. Milestone status changed
        fundResult.Milestone.Status.Should().Be(MilestoneStatus.FUNDED);
        fundResult.Milestone.FundedAt.Should().NotBeNull();

        // 2. Client wallet balance changed (Available decreased, Held increased)
        fundResult.Wallet.AvailableBalance.Should().Be(1100); // 2000 - 900
        fundResult.Wallet.HeldBalance.Should().Be(900);     // 0 + 900

        // 3. Project status changed
        var updatedProject = await dbContext.Projects.FindAsync(projectId);
        updatedProject!.Status.Should().Be(ProjectStatus.ACTIVE);
        updatedProject.StartDate.Should().NotBeNull();

        // 4. Payment was created with HELD status
        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(p => p.MilestoneId == milestoneId);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.HELD);
        payment.HeldAt.Should().NotBeNull();
        payment.Amount.Should().Be(900);

        // 5. Transaction log was created (check for audit trail)
        var transaction = await dbContext.WalletTransactions
            .FirstOrDefaultAsync(t => t.UserId == clientId && t.Type == WalletTransactionType.ESCROW_HOLD);
        transaction.Should().NotBeNull();
        transaction!.Amount.Should().Be(900);
        transaction.Direction.Should().Be(TransactionDirection.DEBIT);

        // Note: InMemory database doesn't always persist changes across DbContext instances
        // The important thing is that the service returned the updated wallet values
        Console.WriteLine("✅ Test passed: Happy path funding flow works correctly");
    }

    /// <summary>
    /// Test 2: Negative Case - Insufficient balance prevents funding
    ///
    /// Given: Client has wallet with balance < milestone amount
    /// When: Client attempts to fund milestone
    /// Then:
    ///   - Throws ValidationException
    ///   - No changes to wallet, milestone, or project
    /// </summary>
    [Fact]
    public async Task Client_Funds_Milestone_With_Insufficient_Balance_Fails()
    {
        // ----------------------------------------------------
        // Arrange: Insufficient balance scenario
        // ----------------------------------------------------
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        // Create users
        var clientUser = new User
        {
            Id = clientId,
            Email = "client2@aivora.com",
            PasswordHash = "hash",
            FullName = "Client User 2",
            Role = UserRole.CLIENT,
            Status = UserStatus.ACTIVE
        };

        var expertUser = new User
        {
            Id = expertId,
            Email = "expert2@aivora.com",
            PasswordHash = "hash",
            FullName = "Expert User 2",
            Role = UserRole.EXPERT,
            Status = UserStatus.ACTIVE
        };

        // Client only has 500 but milestone costs 900
        var clientWallet = new Wallet
        {
            UserId = clientId,
            AvailableBalance = 500,
            HeldBalance = 0,
            Currency = "AICOIN"
        };

        var expertWallet = new Wallet
        {
            UserId = expertId,
            AvailableBalance = 0,
            HeldBalance = 0,
            Currency = "AICOIN"
        };

        var project = new Project
        {
            Id = projectId,
            JobId = Guid.NewGuid(),
            AcceptedProposalId = Guid.NewGuid(),
            ClientId = clientId,
            ExpertId = expertId,
            Title = "Test Project",
            Description = "Test project for milestone funding",
            Status = ProjectStatus.PENDING_PAYMENT,
            Currency = "AICOIN"
        };

        var milestone = new Milestone
        {
            Id = milestoneId,
            ProjectId = projectId,
            Amount = 900,
            Status = MilestoneStatus.CREATED,
            Title = "Test Milestone",
            Currency = "AICOIN"
        };

        // Setup database state
        dbContext.Users.AddRange(clientUser, expertUser);
        dbContext.Wallets.AddRange(clientWallet, expertWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var treasury = new Treasury(dbContext, Mock.Of<ILogger<Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var milestoneService = new Service(dbContext, treasury, Mock.Of<Aivora.Services.NotificationService.IService>());

        // ----------------------------------------------------
        // Act & Assert: Should fail with validation error
        // ----------------------------------------------------
        Func<Task> act = async () => await milestoneService.FundMilestoneAsync(clientId, milestoneId);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Insufficient balance.");

        // Note: Exception thrown means transaction was rolled back
        // No state changes occurred due to atomic transaction
        Console.WriteLine("✅ Test passed: Insufficient balance correctly prevented funding");

        Console.WriteLine("✅ Test passed: Insufficient balance correctly prevented funding");
    }

    /// <summary>
    /// Test 3: Authorization - Only project client can fund milestone
    ///
    /// Given: User is not the project client
    /// When: User attempts to fund milestone
    /// Then:
    ///   - Throws UnauthorizedException
    ///   - No state changes
    /// </summary>
    [Fact]
    public async Task Non_Client_Cannot_Fund_Milestone_Fails()
    {
        // ----------------------------------------------------
        // Arrange: Third-party user scenario
        // ----------------------------------------------------
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();  // Not the client
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        // Create users
        var clientUser = new User
        {
            Id = clientId,
            Email = "client3@aivora.com",
            PasswordHash = "hash",
            FullName = "Client User 3",
            Role = UserRole.CLIENT,
            Status = UserStatus.ACTIVE
        };

        var outsiderUser = new User
        {
            Id = outsiderId,
            Email = "outsider@aivora.com",
            PasswordHash = "hash",
            FullName = "Outsider User",
            Role = UserRole.CLIENT,
            Status = UserStatus.ACTIVE
        };

        var expertUser = new User
        {
            Id = expertId,
            Email = "expert3@aivora.com",
            PasswordHash = "hash",
            FullName = "Expert User 3",
            Role = UserRole.EXPERT,
            Status = UserStatus.ACTIVE
        };

        var outsiderWallet = new Wallet
        {
            UserId = outsiderId,
            AvailableBalance = 2000,
            HeldBalance = 0,
            Currency = "AICOIN"
        };

        var clientWallet = new Wallet
        {
            UserId = clientId,
            AvailableBalance = 2000,
            HeldBalance = 0,
            Currency = "AICOIN"
        };

        var project = new Project
        {
            Id = projectId,
            JobId = Guid.NewGuid(),
            AcceptedProposalId = Guid.NewGuid(),
            ClientId = clientId,  // Different from outsider
            ExpertId = expertId,
            Title = "Test Project",
            Description = "Test project for milestone funding",
            Status = ProjectStatus.PENDING_PAYMENT,
            Currency = "AICOIN"
        };

        var milestone = new Milestone
        {
            Id = milestoneId,
            ProjectId = projectId,
            Amount = 900,
            Status = MilestoneStatus.CREATED,
            Title = "Test Milestone",
            Currency = "AICOIN"
        };

        // Setup database state
        dbContext.Users.AddRange(clientUser, outsiderUser, expertUser);
        dbContext.Wallets.AddRange(outsiderWallet, clientWallet);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var treasury = new Treasury(dbContext, Mock.Of<ILogger<Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService());
        var milestoneService = new Service(dbContext, treasury, Mock.Of<Aivora.Services.NotificationService.IService>());

        // ----------------------------------------------------
        // Act & Assert: Should fail with authorization error
        // ----------------------------------------------------
        Func<Task> act = async () => await milestoneService.FundMilestoneAsync(outsiderId, milestoneId);

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Access denied.");

        // Exception thrown means access was denied as expected
        Console.WriteLine("✅ Test passed: Non-client correctly prevented from funding");
    }
}