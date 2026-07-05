using System;
using System.Threading.Tasks;
using Aivora.Tests.Helpers;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Aivora.api;
using Aivora.Repositories.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.DisputeService;
using Aivora.Services.Exceptions;

namespace Aivora.Tests.IntegrationTests
{
    public class DisputeResolutionTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public DisputeResolutionTests(WebApplicationFactory<Program> factory)
        {
            // Set env vars BEFORE accessing factory (DotNetEnv.Env.Load() runs in Main)
            TestWebHostHelper.SetupTestEnvironment();

            _factory = factory.WithWebHostBuilder(builder =>
            {
                TestWebHostHelper.ConfigureTestHost(builder, "DisputeResolutionTests");
            });
        }

        [Fact]
        public async Task ResolveDispute_ShouldUpdateStatusAndUnlockMilestone()
        {
            // Arrange - Create test data
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AivoraDbContext>();

            // Create users
            var admin = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@test.com",
                FullName = "Admin User",
                Role = UserRole.ADMIN,
                PasswordHash = "hashedpassword"
            };
            dbContext.Users.Add(admin);

            var client = new User
            {
                Id = Guid.NewGuid(),
                Email = "client@test.com",
                FullName = "Client User",
                Role = UserRole.CLIENT,
                PasswordHash = "hashedpassword"
            };
            dbContext.Users.Add(client);

            var expert = new User
            {
                Id = Guid.NewGuid(),
                Email = "expert@test.com",
                FullName = "Expert User",
                Role = UserRole.EXPERT,
                PasswordHash = "hashedpassword"
            };
            dbContext.Users.Add(expert);

            // Create project
            var project = new Project
            {
                Id = Guid.NewGuid(),
                Title = "Test Project",
                Description = "Test Description",
                ClientId = client.Id,
                ExpertId = expert.Id,
                Status = ProjectStatus.DISPUTED,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Projects.Add(project);

            // Create milestone
            var milestone = new Milestone
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Title = "Test Milestone",
                Amount = 1000,
                Status = MilestoneStatus.DISPUTED,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Milestones.Add(milestone);

            // Create payment
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                MilestoneId = milestone.Id,
                ProjectId = project.Id,
                PayerId = client.Id,
                PayeeId = expert.Id,
                Amount = 1000,
                Status = PaymentStatus.HELD,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Payments.Add(payment);

            // Create dispute
            var dispute = new Dispute
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                MilestoneId = milestone.Id,
                PaymentId = payment.Id,
                OpenedBy = client.Id,
                AgainstUserId = expert.Id,
                Reason = "Test dispute",
                Status = DisputeStatus.OPEN,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Disputes.Add(dispute);

            await dbContext.SaveChangesAsync();

            // Get dispute service
            var disputeService = scope.ServiceProvider.GetRequiredService<IService>();

            // Act - Resolve dispute with note only (no financial operations)
            var request = new Aivora.Services.DisputeService.Request.ResolveDisputeRequest
            {
                ResolutionNote = "Resolved via external mediation"
            };

            var response = await disputeService.ResolveDisputeAsync(admin.Id, dispute.Id, request);

            // Assert - Should succeed
            Assert.NotNull(response);
            Assert.Equal(DisputeStatus.RESOLVED.ToString(), response.Status);

            // Verify milestone is now SUBMITTED (unlocked)
            var updatedMilestone = await dbContext.Milestones.FindAsync(milestone.Id);
            Assert.NotNull(updatedMilestone);
            Assert.Equal(MilestoneStatus.SUBMITTED, updatedMilestone.Status);

            // Verify project reverted to ACTIVE
            var updatedProject = await dbContext.Projects.FindAsync(project.Id);
            Assert.NotNull(updatedProject);
            Assert.Equal(ProjectStatus.ACTIVE, updatedProject.Status);
        }

        [Fact]
        public async Task ResolveDispute_AlreadyResolved_ShouldThrowValidation()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AivoraDbContext>();

            var admin = new User { Id = Guid.NewGuid(), Email = "admin2@test.com", FullName = "Admin", Role = UserRole.ADMIN, PasswordHash = "hash" };
            var client = new User { Id = Guid.NewGuid(), Email = "client2@test.com", FullName = "Client", Role = UserRole.CLIENT, PasswordHash = "hash" };
            var expert = new User { Id = Guid.NewGuid(), Email = "expert2@test.com", FullName = "Expert", Role = UserRole.EXPERT, PasswordHash = "hash" };
            dbContext.Users.AddRange(admin, client, expert);

            var project = new Project { Id = Guid.NewGuid(), Title = "Test", ClientId = client.Id, ExpertId = expert.Id, Status = ProjectStatus.DISPUTED };
            dbContext.Projects.Add(project);

            var milestone = new Milestone { Id = Guid.NewGuid(), ProjectId = project.Id, Title = "M1", Amount = 1000, Status = MilestoneStatus.DISPUTED };
            dbContext.Milestones.Add(milestone);

            var payment = new Payment { Id = Guid.NewGuid(), MilestoneId = milestone.Id, ProjectId = project.Id, PayerId = client.Id, PayeeId = expert.Id, Amount = 1000, Status = PaymentStatus.HELD };
            dbContext.Payments.Add(payment);

            var dispute = new Dispute { Id = Guid.NewGuid(), ProjectId = project.Id, MilestoneId = milestone.Id, PaymentId = payment.Id, OpenedBy = client.Id, AgainstUserId = expert.Id, Reason = "Test", Status = DisputeStatus.RESOLVED };
            dbContext.Disputes.Add(dispute);
            await dbContext.SaveChangesAsync();

            var disputeService = scope.ServiceProvider.GetRequiredService<IService>();

            var request = new Aivora.Services.DisputeService.Request.ResolveDisputeRequest
            {
                ResolutionNote = "Try again"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(
                () => disputeService.ResolveDisputeAsync(admin.Id, dispute.Id, request));
        }
    }
}
