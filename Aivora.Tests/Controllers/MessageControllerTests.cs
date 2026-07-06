using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Aivora.api.Controllers;
using BaseRequest = Aivora.Services.Base.Request;
using BaseResponse = Aivora.Services.Base.Response;
using Aivora.Services.MessageService;
using MessageResponse = Aivora.Services.MessageService.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using FluentAssertions;

namespace Aivora.Tests.Controllers;

public class MessageControllerTests
{
    private readonly Mock<IService> _mockMessageService;
    private readonly MessageController _controller;

    public MessageControllerTests()
    {
        _mockMessageService = new Mock<IService>();
        _controller = new MessageController(_mockMessageService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "test-trace-id",
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                        new Claim(ClaimTypes.Role, "ADMIN")
                    }, "Test"))
                }
            }
        };
    }

    [Fact]
    public async Task GetAdminConversations_ShouldCallServiceAndReturnOk()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var pageRequest = new BaseRequest.PageRequest { PageIndex = 1, PageSize = 10 };

        var expectedResult = new BaseResponse.PageResult<MessageResponse.ConversationResponse>
        {
            Items = new List<MessageResponse.ConversationResponse>(),
            PageIndex = 1,
            PageSize = 10,
            TotalItems = 0
        };

        _mockMessageService.Setup(s => s.GetAdminConversationsAsync(jobId, projectId, userId, pageRequest))
            .ReturnsAsync(expectedResult);

        // Act
        var actionResult = await _controller.GetAdminConversations(jobId, projectId, userId, pageRequest);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
        _mockMessageService.Verify(s => s.GetAdminConversationsAsync(jobId, projectId, userId, pageRequest), Times.Once);
    }

    [Fact]
    public async Task GetMessages_ShouldCallServiceAndReturnOk()
    {
        // Arrange
        var conversationId = Guid.NewGuid();
        var pageRequest = new BaseRequest.PageRequest { PageIndex = 1, PageSize = 10 };
        var expectedResult = new BaseResponse.PageResult<MessageResponse.MessageResponse>
        {
            Items = new List<MessageResponse.MessageResponse>(),
            PageIndex = 1,
            PageSize = 10,
            TotalItems = 0
        };

        _mockMessageService.Setup(s => s.GetConversationMessagesAsync(It.IsAny<Guid>(), Aivora.Repositories.Enums.UserRole.ADMIN, conversationId, pageRequest))
            .ReturnsAsync(expectedResult);

        // Act
        var actionResult = await _controller.GetMessages(conversationId, pageRequest);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
        _mockMessageService.Verify(s => s.GetConversationMessagesAsync(It.IsAny<Guid>(), Aivora.Repositories.Enums.UserRole.ADMIN, conversationId, pageRequest), Times.Once);
    }
}
