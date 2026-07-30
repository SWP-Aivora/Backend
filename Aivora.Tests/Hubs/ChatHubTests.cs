using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Aivora.api.Hubs;
using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using MessageRequest = Aivora.Services.MessageService.Request;
using MessageResponse = Aivora.Services.MessageService.Response;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using IMessageService = Aivora.Services.MessageService.IService;

namespace Aivora.Tests.Hubs;

public class ChatHubTests
{
    private readonly Mock<IMessageService> _mockMessageService;
    private readonly Mock<ILogger<ChatHub>> _mockLogger;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly AivoraDbContext _dbContext;

    public ChatHubTests()
    {
        _mockMessageService = new Mock<IMessageService>();
        _mockLogger = new Mock<ILogger<ChatHub>>();
        _mockContext = new Mock<HubCallerContext>();
        _mockClients = new Mock<IHubCallerClients>();
        _mockGroups = new Mock<IGroupManager>();
        _mockClientProxy = new Mock<IClientProxy>();

        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);

        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AivoraDbContext(options);
    }

    private ChatHub CreateHub(ClaimsPrincipal principal)
    {
        _mockContext.Setup(c => c.User).Returns(principal);
        _mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");

        return new ChatHub(_mockMessageService.Object, _dbContext, _mockLogger.Object)
        {
            Context = _mockContext.Object,
            Clients = _mockClients.Object,
            Groups = _mockGroups.Object
        };
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, UserRole role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role.ToString())
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    [Fact]
    public async Task SendMessage_AdminSender_ShouldSucceed()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Role, "ADMIN")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var hub = CreateHub(principal);

        var request = new MessageRequest.SendMessageRequest { ConversationId = conversationId, Content = "Hello from Admin" };
        var expectedResponse = new MessageResponse.MessageResponse
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = adminId,
            SenderName = "Admin",
            SenderRole = UserRole.ADMIN,
            Content = "Hello from Admin",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _mockMessageService.Setup(s => s.SendMessageAsync(adminId, UserRole.ADMIN, request))
            .ReturnsAsync(expectedResponse);

        // Act
        Func<Task> act = async () => await hub.SendMessage(request);

        // Assert
        await act.Should().NotThrowAsync();
        _mockMessageService.Verify(s => s.SendMessageAsync(adminId, UserRole.ADMIN, request), Times.Once);
        _mockClients.Verify(c => c.Group(conversationId.ToString()), Times.Once);
        _mockClientProxy.Verify(p => p.SendCoreAsync("ReceiveMessage", It.Is<object[]>(o => o[0] == expectedResponse), default), Times.Once);
    }

    [Fact]
    public async Task SendMessage_ContentOverLimit_ThrowsHubExceptionAndDoesNotCallService()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Role, "ADMIN")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var hub = CreateHub(principal);

        var request = new MessageRequest.SendMessageRequest { ConversationId = conversationId, Content = new string('a', 4001) };

        // Act
        Func<Task> act = async () => await hub.SendMessage(request);

        // Assert
        await act.Should().ThrowAsync<HubException>();
        _mockMessageService.Verify(s => s.SendMessageAsync(It.IsAny<Guid>(), It.IsAny<UserRole>(), It.IsAny<MessageRequest.SendMessageRequest>()), Times.Never);
    }

    [Fact]
    public async Task JoinConversation_AdminSender_ShouldCallEnsureConversationParticipant()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
            new Claim(ClaimTypes.Role, "ADMIN")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var hub = CreateHub(principal);

        _mockMessageService.Setup(s => s.EnsureConversationParticipantAsync(adminId, UserRole.ADMIN, conversationId))
            .Returns(Task.CompletedTask);

        // Act
        Func<Task> act = async () => await hub.JoinConversation(conversationId);

        // Assert
        await act.Should().NotThrowAsync();
        _mockMessageService.Verify(s => s.EnsureConversationParticipantAsync(adminId, UserRole.ADMIN, conversationId), Times.Once);
        _mockGroups.Verify(g => g.AddToGroupAsync("test-connection-id", conversationId.ToString(), default), Times.Once);
    }

    [Fact]
    public async Task JoinProject_ByOutsider_ThrowsHubExceptionAndDoesNotAddToGroup()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var project = new Project { ClientId = clientId, ExpertId = expertId, Title = "P1" };
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        var hub = CreateHub(CreatePrincipal(outsiderId, UserRole.EXPERT));

        // Act
        Func<Task> act = async () => await hub.JoinProject(project.Id);

        // Assert
        await act.Should().ThrowAsync<HubException>();
        _mockGroups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task JoinProject_ByClient_AddsToProjectGroup()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var project = new Project { ClientId = clientId, ExpertId = expertId, Title = "P1" };
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        var hub = CreateHub(CreatePrincipal(clientId, UserRole.CLIENT));

        // Act
        Func<Task> act = async () => await hub.JoinProject(project.Id);

        // Assert
        await act.Should().NotThrowAsync();
        _mockGroups.Verify(g => g.AddToGroupAsync("test-connection-id", $"project-{project.Id}", default), Times.Once);
    }

    [Fact]
    public async Task JoinProject_ByAdmin_AddsToProjectGroupEvenIfNotParticipant()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var project = new Project { ClientId = clientId, ExpertId = expertId, Title = "P1" };
        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();

        var hub = CreateHub(CreatePrincipal(adminId, UserRole.ADMIN));

        // Act
        Func<Task> act = async () => await hub.JoinProject(project.Id);

        // Assert
        await act.Should().NotThrowAsync();
        _mockGroups.Verify(g => g.AddToGroupAsync("test-connection-id", $"project-{project.Id}", default), Times.Once);
    }
}
