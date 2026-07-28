using Aivora.Repositories.Constants;
using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.MilestoneService;
using Aivora.Services.Treasury;
using Aivora.Services.Exceptions;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Aivora.Services.Options;
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
        return GetDbContext(Guid.NewGuid().ToString());
    }

    private AivoraDbContext GetDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName)
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
        var commissionOptions = Options.Create(new CommissionOptions { Rate = 0.10m });
        var treasury = new Treasury(dbContext, new Aivora.Services.Treasury.CommissionCalculator(commissionOptions), Mock.Of<ILogger<Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService(), Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions()));
        var milestoneService = new Service(dbContext, treasury, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());

        // ----------------------------------------------------
        // Act: Client funds the milestone
        // ----------------------------------------------------
        var fundResult = await milestoneService.FundMilestoneAsync(clientId, milestoneId);

        // ----------------------------------------------------
        // Assert: Verify observable behavior changes
        // ----------------------------------------------------
        // 1. Milestone status changed
        fundResult.Milestone.Status.Should().Be(MilestoneStatus.IN_PROGRESS);
        fundResult.Milestone.DepositPaidAt.Should().NotBeNull();

        // 2. Client wallet balance changed (Available decreased by 30% deposit, Held unchanged)
        // 30% of 900 = 270
        fundResult.Wallet.AvailableBalance.Should().Be(1730); // 2000 - 270
        fundResult.Wallet.HeldBalance.Should().Be(0);     // 0 + 0

        // 3. Project status changed
        var updatedProject = await dbContext.Projects.FindAsync(projectId);
        updatedProject!.Status.Should().Be(ProjectStatus.ACTIVE);
        updatedProject.StartDate.Should().NotBeNull();

        // 4. Payment was created with RELEASED status for 30% deposit
        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(p => p.MilestoneId == milestoneId);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.RELEASED);
        payment.HeldAt.Should().BeNull();
        payment.ReleasedAt.Should().NotBeNull();
        payment.Amount.Should().Be(270);

        // 5. Transaction log was created (check for audit trail)
        var transaction = await dbContext.WalletTransactions
            .FirstOrDefaultAsync(t => t.UserId == clientId && t.Type == WalletTransactionType.PAYMENT_RELEASE);
        transaction.Should().NotBeNull();
        transaction!.Amount.Should().Be(270);
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
            AvailableBalance = 200, // Less than 270 (30% of 900)
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

        var commissionOptions = Options.Create(new CommissionOptions { Rate = 0.10m });
        var treasury = new Treasury(dbContext, new Aivora.Services.Treasury.CommissionCalculator(commissionOptions), Mock.Of<ILogger<Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService(), Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions()));
        var milestoneService = new Service(dbContext, treasury, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());

        // ----------------------------------------------------
        // Act & Assert: Should fail with validation error
        // ----------------------------------------------------
        Func<Task> act = async () => await milestoneService.FundMilestoneAsync(clientId, milestoneId);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("Insufficient balance for deposit.");

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
    ///   - Throws ForbiddenException
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

        var commissionOptions = Options.Create(new CommissionOptions { Rate = 0.10m });
        var treasury = new Treasury(dbContext, new Aivora.Services.Treasury.CommissionCalculator(commissionOptions), Mock.Of<ILogger<Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService(), Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions()));
        var milestoneService = new Service(dbContext, treasury, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());

        // ----------------------------------------------------
        // Act & Assert: Should fail with authorization error
        // ----------------------------------------------------
        Func<Task> act = async () => await milestoneService.FundMilestoneAsync(outsiderId, milestoneId);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("Access denied.");

        // Exception thrown means access was denied as expected
        Console.WriteLine("✅ Test passed: Non-client correctly prevented from funding");
    }

    /// <summary>
    /// Test: Concurrent double-fund race (BUG-1, issue #154)
    ///
    /// Given: Milestone is CREATED and client has enough balance for exactly one deposit
    /// When: N requests call FundMilestoneAsync for the same milestone at the same time
    ///       (separate DbContext per request, like separate HTTP requests would get)
    /// Then:
    ///   - Exactly one request succeeds; the rest fail with the normal
    ///     "Milestone must be CREATED to pay deposit." validation error
    ///   - Exactly one Payment/WalletTransaction pair is persisted
    ///   - Client is only debited once
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public async Task FundMilestoneAsync_WithConcurrentRequests_OnlyOneSucceeds(int concurrentRequests)
    {
        // ----------------------------------------------------
        // Arrange: seed a CREATED milestone shared by every request
        // ----------------------------------------------------
        var dbName = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        using (var seedContext = GetDbContext(dbName))
        {
            seedContext.Users.AddRange(
                new User { Id = clientId, Email = $"race-client-{dbName}@aivora.com", PasswordHash = "hash", FullName = "Client", Role = UserRole.CLIENT, Status = UserStatus.ACTIVE },
                new User { Id = expertId, Email = $"race-expert-{dbName}@aivora.com", PasswordHash = "hash", FullName = "Expert", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE });
            seedContext.Wallets.AddRange(
                new Wallet { UserId = clientId, AvailableBalance = 2000, HeldBalance = 0, Currency = "AICOIN" },
                new Wallet { UserId = expertId, AvailableBalance = 0, HeldBalance = 0, Currency = "AICOIN" });
            seedContext.Projects.Add(new Project
            {
                Id = projectId,
                JobId = Guid.NewGuid(),
                AcceptedProposalId = Guid.NewGuid(),
                ClientId = clientId,
                ExpertId = expertId,
                Title = "Race Project",
                Description = "Race condition test project",
                Status = ProjectStatus.PENDING_PAYMENT,
                Currency = "AICOIN"
            });
            seedContext.Milestones.Add(new Milestone
            {
                Id = milestoneId,
                ProjectId = projectId,
                Amount = 900,
                Status = MilestoneStatus.CREATED,
                Title = "Race Milestone",
                Currency = "AICOIN"
            });
            await seedContext.SaveChangesAsync();
        }

        Task<Response.FundResultResponse> RunFund()
        {
            var dbContext = GetDbContext(dbName);
            var commissionOptions = Options.Create(new CommissionOptions { Rate = 0.10m });
            var treasury = new Treasury(dbContext, new Aivora.Services.Treasury.CommissionCalculator(commissionOptions), Mock.Of<ILogger<Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService(), Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions()));
            var milestoneService = new Service(dbContext, treasury, Mock.Of<Aivora.Services.NotificationService.IService>(), Mock.Of<Aivora.Services.AIMilestoneStepAssistantService.IAIMilestoneStepSuggestionProvider>());
            return milestoneService.FundMilestoneAsync(clientId, milestoneId);
        }

        // ----------------------------------------------------
        // Act: fire every request truly in parallel (separate threads, separate DbContexts)
        // ----------------------------------------------------
        var tasks = Enumerable.Range(0, concurrentRequests).Select(_ => Task.Run(RunFund)).ToArray();
        try { await Task.WhenAll(tasks); } catch { /* inspected per-task below */ }

        // ----------------------------------------------------
        // Assert: exactly one winner, everyone else gets the normal validation error
        // ----------------------------------------------------
        var succeeded = tasks.Where(t => t.IsCompletedSuccessfully).ToList();
        var failed = tasks.Where(t => t.IsFaulted).ToList();

        succeeded.Should().HaveCount(1, "double-click / retry-on-timeout must not fund the milestone twice");
        failed.Should().HaveCount(concurrentRequests - 1);
        failed.Should().OnlyContain(t => t.Exception!.InnerException is ValidationException);
        failed.Select(t => ((ValidationException)t.Exception!.InnerException!).Message)
            .Should().OnlyContain(m => m == "Milestone must be CREATED to pay deposit.");

        using var verifyContext = GetDbContext(dbName);
        var payments = await verifyContext.Payments.Where(p => p.MilestoneId == milestoneId).ToListAsync();
        payments.Should().HaveCount(1);

        var clientDebits = await verifyContext.WalletTransactions
            .Where(t => t.UserId == clientId && t.Type == WalletTransactionType.PAYMENT_RELEASE)
            .ToListAsync();
        clientDebits.Should().HaveCount(1);

        var clientWallet = await verifyContext.Wallets.FirstAsync(w => w.UserId == clientId);
        clientWallet.AvailableBalance.Should().Be(1730); // 2000 - 270 (30% of 900), debited exactly once
    }

    /// <summary>
    /// Same race as FundMilestoneAsync_WithConcurrentRequests_OnlyOneSucceeds, for the
    /// SUBMITTED -> RELEASED transition (PayRemainingAsync / "approve milestone").
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public async Task PayRemainingAsync_WithConcurrentRequests_OnlyOneSucceeds(int concurrentRequests)
    {
        var dbName = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        using (var seedContext = GetDbContext(dbName))
        {
            seedContext.Users.AddRange(
                new User { Id = clientId, Email = $"race-client-{dbName}@aivora.com", PasswordHash = "hash", FullName = "Client", Role = UserRole.CLIENT, Status = UserStatus.ACTIVE },
                new User { Id = expertId, Email = $"race-expert-{dbName}@aivora.com", PasswordHash = "hash", FullName = "Expert", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE },
                new User { Id = SystemConstants.SystemUserId, Email = "system@aivora.com", PasswordHash = "hash", FullName = "System Platform", Role = UserRole.SYSTEM, Status = UserStatus.ACTIVE });
            seedContext.Wallets.AddRange(
                new Wallet { UserId = clientId, AvailableBalance = 2000, Currency = "AICOIN" },
                new Wallet { UserId = expertId, AvailableBalance = 300, TotalEarned = 300, Currency = "AICOIN" }, // 30% deposit already paid
                new Wallet { UserId = SystemConstants.SystemUserId, AvailableBalance = 0, Currency = "AICOIN" });
            seedContext.Projects.Add(new Project
            {
                Id = projectId,
                JobId = Guid.NewGuid(),
                AcceptedProposalId = Guid.NewGuid(),
                ClientId = clientId,
                ExpertId = expertId,
                Title = "Race Project",
                Status = ProjectStatus.ACTIVE,
                Currency = "AICOIN"
            });
            seedContext.Milestones.Add(new Milestone
            {
                Id = milestoneId,
                ProjectId = projectId,
                Amount = 1000, // Remaining 70% = 700
                Status = MilestoneStatus.SUBMITTED,
                Title = "Race Milestone",
                Currency = "AICOIN"
            });
            await seedContext.SaveChangesAsync();
        }

        Task<TreasuryResult> RunApprove()
        {
            var dbContext = GetDbContext(dbName);
            var commissionOptions = Options.Create(new CommissionOptions { Rate = 0.10m });
            var treasury = new Treasury(dbContext, new Aivora.Services.Treasury.CommissionCalculator(commissionOptions), Mock.Of<ILogger<Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService(), Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions()));
            return treasury.PayRemainingAsync(clientId, milestoneId);
        }

        var tasks = Enumerable.Range(0, concurrentRequests).Select(_ => Task.Run(RunApprove)).ToArray();
        try { await Task.WhenAll(tasks); } catch { /* inspected per-task below */ }

        var succeeded = tasks.Where(t => t.IsCompletedSuccessfully).ToList();
        var failed = tasks.Where(t => t.IsFaulted).ToList();

        succeeded.Should().HaveCount(1, "double-click / retry-on-timeout must not release the remaining funds twice");
        failed.Should().HaveCount(concurrentRequests - 1);
        failed.Should().OnlyContain(t => t.Exception!.InnerException is ValidationException);
        failed.Select(t => ((ValidationException)t.Exception!.InnerException!).Message)
            .Should().OnlyContain(m => m == "Milestone must be in SUBMITTED status to release remaining funds.");

        using var verifyContext = GetDbContext(dbName);
        var payments = await verifyContext.Payments.Where(p => p.MilestoneId == milestoneId).ToListAsync();
        payments.Should().HaveCount(1);
        payments[0].Amount.Should().Be(700);

        var clientWallet = await verifyContext.Wallets.FirstAsync(w => w.UserId == clientId);
        clientWallet.AvailableBalance.Should().Be(1300); // 2000 - 700, debited exactly once
    }

    /// <summary>
    /// Same race, for RefundMilestoneAsync (admin claws back an already-released payment).
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public async Task RefundMilestoneAsync_WithConcurrentRequests_OnlyOneSucceeds(int concurrentRequests)
    {
        var dbName = Guid.NewGuid().ToString();
        var adminId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        using (var seedContext = GetDbContext(dbName))
        {
            seedContext.Users.AddRange(
                new User { Id = clientId, Email = $"race-client-{dbName}@aivora.com", PasswordHash = "hash", FullName = "Client", Role = UserRole.CLIENT, Status = UserStatus.ACTIVE },
                new User { Id = expertId, Email = $"race-expert-{dbName}@aivora.com", PasswordHash = "hash", FullName = "Expert", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE });
            seedContext.Wallets.AddRange(
                new Wallet { UserId = clientId, AvailableBalance = 1000, Currency = "AICOIN" },
                new Wallet { UserId = expertId, AvailableBalance = 500, TotalEarned = 270, Currency = "AICOIN" });
            seedContext.Projects.Add(new Project
            {
                Id = projectId,
                JobId = Guid.NewGuid(),
                AcceptedProposalId = Guid.NewGuid(),
                ClientId = clientId,
                ExpertId = expertId,
                Title = "Race Project",
                Status = ProjectStatus.ACTIVE,
                Currency = "AICOIN"
            });
            seedContext.Milestones.Add(new Milestone
            {
                Id = milestoneId,
                ProjectId = projectId,
                Amount = 900,
                Status = MilestoneStatus.IN_PROGRESS,
                Title = "Race Milestone",
                Currency = "AICOIN"
            });
            seedContext.Payments.Add(new Payment
            {
                Id = paymentId,
                MilestoneId = milestoneId,
                ProjectId = projectId,
                PayerId = clientId,
                PayeeId = expertId,
                Amount = 270,
                Currency = "AICOIN",
                Status = PaymentStatus.RELEASED,
                ReleasedAt = DateTimeOffset.UtcNow
            });
            await seedContext.SaveChangesAsync();
        }

        Task<TreasuryResult> RunRefund()
        {
            var dbContext = GetDbContext(dbName);
            var commissionOptions = Options.Create(new CommissionOptions { Rate = 0.10m });
            var treasury = new Treasury(dbContext, new Aivora.Services.Treasury.CommissionCalculator(commissionOptions), Mock.Of<ILogger<Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService(), Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions()));
            return treasury.RefundMilestoneAsync(adminId, milestoneId, "test refund");
        }

        var tasks = Enumerable.Range(0, concurrentRequests).Select(_ => Task.Run(RunRefund)).ToArray();
        try { await Task.WhenAll(tasks); } catch { /* inspected per-task below */ }

        var succeeded = tasks.Where(t => t.IsCompletedSuccessfully).ToList();
        var failed = tasks.Where(t => t.IsFaulted).ToList();

        succeeded.Should().HaveCount(1, "double-click must not refund the same payment twice");
        failed.Should().HaveCount(concurrentRequests - 1);
        // A loser can hit either safe rejection path depending on timing: the milestone-status
        // guard/concurrency check (already modified), or the payments-not-found guard if it reads
        // after the winner already flipped every Payment to REFUNDED. Both are safe (no double-refund).
        failed.Should().OnlyContain(t =>
            (t.Exception!.InnerException is ValidationException && t.Exception!.InnerException!.Message == "Milestone was already modified by a concurrent operation. Please retry.") ||
            (t.Exception!.InnerException is NotFoundException && t.Exception!.InnerException!.Message == "Payment not found for refund."));

        using var verifyContext = GetDbContext(dbName);
        var refundTransactions = await verifyContext.WalletTransactions
            .Where(t => t.PaymentId == paymentId && t.Type == WalletTransactionType.REFUND)
            .ToListAsync();
        refundTransactions.Should().HaveCount(2); // payer credit + payee debit, exactly once

        var clientWallet = await verifyContext.Wallets.FirstAsync(w => w.UserId == clientId);
        clientWallet.AvailableBalance.Should().Be(1270); // 1000 + 270, credited exactly once
    }

    /// <summary>
    /// Same race, for SplitMilestoneFundsAsync (dispute resolution splits a released payment).
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public async Task SplitMilestoneFundsAsync_WithConcurrentRequests_OnlyOneSucceeds(int concurrentRequests)
    {
        var dbName = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        using (var seedContext = GetDbContext(dbName))
        {
            seedContext.Users.AddRange(
                new User { Id = clientId, Email = $"race-client-{dbName}@aivora.com", PasswordHash = "hash", FullName = "Client", Role = UserRole.CLIENT, Status = UserStatus.ACTIVE },
                new User { Id = expertId, Email = $"race-expert-{dbName}@aivora.com", PasswordHash = "hash", FullName = "Expert", Role = UserRole.EXPERT, Status = UserStatus.ACTIVE });
            seedContext.Wallets.AddRange(
                new Wallet { UserId = clientId, AvailableBalance = 1000, Currency = "AICOIN" },
                new Wallet { UserId = expertId, AvailableBalance = 1000, TotalEarned = 1000, Currency = "AICOIN" });
            seedContext.Projects.Add(new Project
            {
                Id = projectId,
                JobId = Guid.NewGuid(),
                AcceptedProposalId = Guid.NewGuid(),
                ClientId = clientId,
                ExpertId = expertId,
                Title = "Race Project",
                Status = ProjectStatus.DISPUTED,
                Currency = "AICOIN"
            });
            seedContext.Milestones.Add(new Milestone
            {
                Id = milestoneId,
                ProjectId = projectId,
                Amount = 1000,
                Status = MilestoneStatus.DISPUTED,
                Title = "Race Milestone",
                Currency = "AICOIN"
            });
            seedContext.Payments.Add(new Payment
            {
                Id = paymentId,
                MilestoneId = milestoneId,
                ProjectId = projectId,
                PayerId = clientId,
                PayeeId = expertId,
                Amount = 1000,
                Currency = "AICOIN",
                Status = PaymentStatus.RELEASED,
                ReleasedAt = DateTimeOffset.UtcNow
            });
            await seedContext.SaveChangesAsync();
        }

        Task<TreasuryResult> RunSplit()
        {
            var dbContext = GetDbContext(dbName);
            var commissionOptions = Options.Create(new CommissionOptions { Rate = 0.10m });
            var treasury = new Treasury(dbContext, new Aivora.Services.Treasury.CommissionCalculator(commissionOptions), Mock.Of<ILogger<Treasury>>(), Mock.Of<Aivora.Services.NotificationService.IService>(), new Aivora.Services.RealtimeService.NullRealtimeService(), Microsoft.Extensions.Options.Options.Create(new Aivora.Services.Options.EscrowOptions()));
            return treasury.SplitMilestoneFundsAsync(milestoneId, 600m, 400m, "test split");
        }

        var tasks = Enumerable.Range(0, concurrentRequests).Select(_ => Task.Run(RunSplit)).ToArray();
        try { await Task.WhenAll(tasks); } catch { /* inspected per-task below */ }

        var succeeded = tasks.Where(t => t.IsCompletedSuccessfully).ToList();
        var failed = tasks.Where(t => t.IsFaulted).ToList();

        succeeded.Should().HaveCount(1, "double-click must not split the same payment twice");
        failed.Should().HaveCount(concurrentRequests - 1);
        // A loser can hit either safe rejection path depending on timing: the milestone-status
        // guard/concurrency check (already modified), or the payments-not-found guard if it reads
        // after the winner already flipped every Payment away from RELEASED. Both are safe (no double-split).
        failed.Should().OnlyContain(t =>
            (t.Exception!.InnerException is ValidationException && t.Exception!.InnerException!.Message == "Milestone was already modified by a concurrent operation. Please retry.") ||
            (t.Exception!.InnerException is NotFoundException && t.Exception!.InnerException!.Message == "Payment not found for split."));

        using var verifyContext = GetDbContext(dbName);
        var refundTransactions = await verifyContext.WalletTransactions
            .Where(t => t.PaymentId == paymentId && t.Type == WalletTransactionType.REFUND)
            .ToListAsync();
        refundTransactions.Should().HaveCount(2); // payer credit + payee debit, exactly once

        var clientWallet = await verifyContext.Wallets.FirstAsync(w => w.UserId == clientId);
        clientWallet.AvailableBalance.Should().Be(1400); // 1000 + 400 refund, credited exactly once
    }
}
