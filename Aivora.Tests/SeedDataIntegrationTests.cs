using Aivora.Repositories.Data;
using Aivora.Repositories.Enums;
using Aivora.Services.HiringService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aivora.Tests;

public class SeedDataIntegrationTests
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
    public async Task AcceptProposal_UsingSeedData_Succeeds()
    {
        // 1. Arrange: Khởi tạo DB và Seed Data
        var dbContext = GetDbContext();
        await SeedData.Initialize(dbContext);

        // Lấy thông tin từ SeedData: 
        // - Job: "Build a Customer Support Chatbot for a SaaS Product" (Client: clientStartup)
        // - Proposal của expertSeniorAI
        var clientStartup = await dbContext.Users.FirstAsync(u => u.Email == "client.startup@demo.com");
        var expertSeniorAI = await dbContext.Users.FirstAsync(u => u.Email == "expert.senior.ai@demo.com");
        var job = await dbContext.JobPosts.FirstAsync(j => j.Title.Contains("Customer Support Chatbot"));
        var proposal = await dbContext.Proposals.FirstAsync(p => p.JobId == job.Id && p.ExpertId == expertSeniorAI.Id);

        var hiringService = new HiringService(new Aivora.Repositories.Repositories.Proposals.ProposalRepository(dbContext), new Aivora.Repositories.Repositories.Projects.ProjectRepository(dbContext), new Aivora.Repositories.Data.EfUnitOfWork(dbContext));

        // 2. Act: Thực hiện chấp nhận Proposal
        var result = await hiringService.AcceptProposalAsync(clientStartup.Id, proposal.Id);

        // 3. Assert: Kiểm tra kết quả
        result.AcceptedProposalId.Should().Be(proposal.Id);
        result.Status.Should().Be(ProjectStatus.PENDING_PAYMENT.ToString());

        // Kiểm tra trạng thái trong DB
        var updatedJob = await dbContext.JobPosts.FindAsync(job.Id);
        updatedJob!.Status.Should().Be(JobStatus.IN_PROGRESS);

        var otherProposals = await dbContext.Proposals
            .Where(p => p.JobId == job.Id && p.Id != proposal.Id)
            .ToListAsync();

        otherProposals.Should().AllSatisfy(p => p.Status.Should().Be(ProposalStatus.REJECTED));
    }
}
