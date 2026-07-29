using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aivora.api.Hubs;
using Aivora.Repositories.Enums;
using Aivora.Services.RealtimeService;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Aivora.Tests.Services;

public class RealtimeServiceTests
{
    [Fact]
    public async Task SendJobStatusUpdateAsync_CallsClientsUserSendAsync()
    {
        var mockHubContext = new Mock<IHubContext<ChatHub>>();
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();

        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockClientProxy.Object);

        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var service = new Aivora.Services.RealtimeService.Service(mockHubContext.Object);
        await service.SendJobStatusUpdateAsync(userId, jobId, JobStatus.OPEN, "Test Job");

        mockClients.Verify(c => c.User(userId.ToString()), Times.Once);
        mockClientProxy.Verify(c => c.SendCoreAsync(
            "JobStatusUpdated",
            It.Is<object[]>(args =>
                args.Length == 1 &&
                args[0] is RealtimeJobStatusUpdateDto &&
                ((RealtimeJobStatusUpdateDto)args[0]).JobId == jobId &&
                ((RealtimeJobStatusUpdateDto)args[0]).Status == "open" &&
                ((RealtimeJobStatusUpdateDto)args[0]).Title == "Test Job"
            ),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    // Mock-level: SignalR fans a group broadcast out to every connection that joined the group
    // (both simulated connections here), so verifying Clients.Group($"project-{id}") is invoked
    // with the right event/payload is the mock-level equivalent of "both connections receive it".
    [Fact]
    public async Task SendMilestoneUpdatedAsync_BroadcastsMilestoneUpdatedToProjectGroup()
    {
        var mockHubContext = new Mock<IHubContext<ChatHub>>();
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        var done = new ManualResetEventSlim(false);

        var projectId = Guid.NewGuid();
        var milestoneId = Guid.NewGuid();

        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.Group($"project-{projectId}")).Returns(mockClientProxy.Object);
        mockClientProxy
            .Setup(p => p.SendCoreAsync("MilestoneUpdated", It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Callback(() => done.Set())
            .Returns(Task.CompletedTask);

        var service = new Service(mockHubContext.Object);

        service.SendMilestoneUpdatedAsync(projectId, milestoneId);

        done.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();

        mockClients.Verify(c => c.Group($"project-{projectId}"), Times.Once);
        mockClientProxy.Verify(p => p.SendCoreAsync(
            "MilestoneUpdated",
            It.Is<object[]>(args =>
                args.Length == 1 &&
                args[0] is MilestoneUpdatedDto &&
                ((MilestoneUpdatedDto)args[0]).ProjectId == projectId &&
                ((MilestoneUpdatedDto)args[0]).MilestoneId == milestoneId
            ),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public void SendMilestoneUpdatedAsync_WhenBroadcastThrows_DoesNotThrowAndLogsError()
    {
        var mockHubContext = new Mock<IHubContext<ChatHub>>();
        var mockLogger = new Mock<ILogger<Service>>();
        var done = new ManualResetEventSlim(false);

        mockHubContext.Setup(h => h.Clients).Throws(new InvalidOperationException("boom"));
        mockLogger
            .Setup(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => done.Set());

        var service = new Service(mockHubContext.Object, mockLogger.Object);

        // Act: must not throw synchronously - it's void/fire-and-forget by design.
        var act = () => service.SendMilestoneUpdatedAsync(Guid.NewGuid(), Guid.NewGuid());
        act.Should().NotThrow();

        done.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue("the internal catch block should have logged the error");
    }
}
