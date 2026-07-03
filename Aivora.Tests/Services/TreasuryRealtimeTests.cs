using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Treasury;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class TreasuryRealtimeTests
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
    public async Task SyncProjectStatusAsync_WhenAllSettled_CallsRealtimeServiceWithCompleted()
    {
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();

        var job = new JobPost { Title = "Job Title", Status = JobStatus.IN_PROGRESS, ClientId = clientId, OriginalDescription = "D" };
        var project = new Project { Job = job, ClientId = clientId, ExpertId = expertId, Status = ProjectStatus.ACTIVE, Title = "Project Title" };
        var milestone = new Milestone { Project = project, Title = "M1", Amount = 100, Status = MilestoneStatus.RELEASED };

        dbContext.JobPosts.Add(job);
        dbContext.Projects.Add(project);
        dbContext.Milestones.Add(milestone);
        await dbContext.SaveChangesAsync();

        var mockRealtime = new Mock<Aivora.Services.RealtimeService.IService>();
        var treasury = new Aivora.Services.Treasury.Treasury(
            dbContext,
            Mock.Of<ILogger<Aivora.Services.Treasury.Treasury>>(),
            Mock.Of<Aivora.Services.NotificationService.IService>(),
            mockRealtime.Object
        );

        await treasury.SyncProjectStatusAsync(project.Id);

        mockRealtime.Verify(r => r.SendJobStatusUpdateToUsersAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Contains(clientId) && ids.Contains(expertId)),
            job.Id,
            JobStatus.COMPLETED,
            "Job Title"
        ), Times.Once);
    }
}
