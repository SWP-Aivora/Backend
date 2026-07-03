using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aivora.api.Hubs;
using Aivora.Repositories.Enums;
using Aivora.Services.RealtimeService;
using Microsoft.AspNetCore.SignalR;
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
}
