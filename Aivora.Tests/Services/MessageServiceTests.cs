using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.MessageService;
using Aivora.Services.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;


namespace Aivora.Tests.Services;

public class MessageServiceTests
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
    public async Task SendMessageAsync_CreatesMessageAndUpdatesConversation()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x", Role = UserRole.CLIENT };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x", Role = UserRole.EXPERT };
        var conversation = new Conversation { Id = conversationId, ClientId = clientId, ExpertId = expertId };

        dbContext.Users.AddRange(client, expert);
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);
        var request = new Request.SendMessageRequest
        {
            ConversationId = conversationId,
            Content = "Hello from client"
        };

        // Act
        var result = await service.SendMessageAsync(clientId, UserRole.CLIENT, request);

        // Assert
        result.Content.Should().Be("Hello from client");
        result.SenderId.Should().Be(clientId);

        var msgInDb = await dbContext.Messages.FirstOrDefaultAsync(m => m.ConversationId == conversationId);
        msgInDb.Should().NotBeNull();
        msgInDb!.Content.Should().Be("Hello from client");

        var updatedConv = await dbContext.Conversations.FindAsync(conversationId);
        updatedConv!.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SendMessageAsync_ContentOverMaxLength_ThrowsValidationAndDoesNotPersist()
    {
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x", Role = UserRole.CLIENT };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x", Role = UserRole.EXPERT };
        var conversation = new Conversation { Id = conversationId, ClientId = clientId, ExpertId = expertId };

        dbContext.Users.AddRange(client, expert);
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);
        var request = new Request.SendMessageRequest
        {
            ConversationId = conversationId,
            Content = new string('a', Service.MaxContentLength + 1)
        };

        var act = () => service.SendMessageAsync(clientId, UserRole.CLIENT, request);

        await act.Should().ThrowAsync<ValidationException>();
        (await dbContext.Messages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SendMessageAsync_ContentAtMaxLength_Succeeds()
    {
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x", Role = UserRole.CLIENT };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x", Role = UserRole.EXPERT };
        var conversation = new Conversation { Id = conversationId, ClientId = clientId, ExpertId = expertId };

        dbContext.Users.AddRange(client, expert);
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);
        var request = new Request.SendMessageRequest
        {
            ConversationId = conversationId,
            Content = new string('a', Service.MaxContentLength)
        };

        var result = await service.SendMessageAsync(clientId, UserRole.CLIENT, request);

        result.Content.Should().HaveLength(Service.MaxContentLength);
    }

    [Fact]
    public async Task SendMessageAsync_AttachmentOnlyWithNullContent_Succeeds()
    {
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x", Role = UserRole.CLIENT };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x", Role = UserRole.EXPERT };
        var conversation = new Conversation { Id = conversationId, ClientId = clientId, ExpertId = expertId };

        dbContext.Users.AddRange(client, expert);
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);
        var request = new Request.SendMessageRequest
        {
            ConversationId = conversationId,
            Content = null,
            AttachmentUrl = "https://example.com/file.png"
        };

        var result = await service.SendMessageAsync(clientId, UserRole.CLIENT, request);

        result.Content.Should().BeNull();
        result.AttachmentUrl.Should().Be("https://example.com/file.png");
    }

    [Fact]
    public async Task MarkAsReadAsync_UpdatesOnlyIncomingMessages()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x", Role = UserRole.CLIENT };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x", Role = UserRole.EXPERT };
        var conversation = new Conversation { Id = conversationId, ClientId = clientId, ExpertId = expertId };
        var msg1 = new Message { ConversationId = conversationId, SenderId = expertId, Content = "Msg from expert", IsRead = false };
        var msg2 = new Message { ConversationId = conversationId, SenderId = clientId, Content = "Msg from client", IsRead = false };

        dbContext.Users.AddRange(client, expert);
        dbContext.Conversations.Add(conversation);
        dbContext.Messages.AddRange(msg1, msg2);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);

        // Act
        await service.MarkAsReadAsync(clientId, conversationId);

        // Assert
        var updatedMsg1 = await dbContext.Messages.FindAsync(msg1.Id);
        updatedMsg1!.IsRead.Should().BeTrue();
        updatedMsg1.ReadAt.Should().NotBeNull();

        var updatedMsg2 = await dbContext.Messages.FindAsync(msg2.Id);
        updatedMsg2!.IsRead.Should().BeFalse(); // Client's own message shouldn't be marked read by client
        updatedMsg2.ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task GetConversationMessagesAsync_Admin_WithOpenDispute_Success()
    {
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var conversation = new Conversation { Id = conversationId, ClientId = clientId, ExpertId = expertId, ProjectId = projectId };
        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            OpenedBy = clientId,
            AgainstUserId = expertId,
            Status = DisputeStatus.OPEN,
            Reason = "Test"
        };
        var msg = new Message { ConversationId = conversationId, SenderId = clientId, Content = "Msg" };
        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x", Role = UserRole.CLIENT };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x", Role = UserRole.EXPERT };
        var admin = new User { Id = adminId, FullName = "Admin User", Email = "admin@t.com", PasswordHash = "x", Role = UserRole.ADMIN };

        dbContext.Users.AddRange(client, expert, admin);
        dbContext.Conversations.Add(conversation);
        dbContext.Disputes.Add(dispute);
        dbContext.Messages.Add(msg);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);

        var result = await service.GetConversationMessagesAsync(adminId, UserRole.ADMIN, conversationId, new Aivora.Services.Base.Request.PageRequest { PageIndex = 1, PageSize = 10 });

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetConversationMessagesAsync_Admin_NoDisputeButWithProjectContext_Success()
    {
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var conversation = new Conversation { Id = conversationId, ClientId = clientId, ExpertId = expertId, ProjectId = projectId };
        var admin = new User { Id = adminId, FullName = "Admin User", Email = "admin@t.com", PasswordHash = "x", Role = UserRole.ADMIN };
        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x", Role = UserRole.CLIENT };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x", Role = UserRole.EXPERT };

        dbContext.Users.AddRange(admin, client, expert);
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);

        var result = await service.GetConversationMessagesAsync(adminId, UserRole.ADMIN, conversationId, new Aivora.Services.Base.Request.PageRequest { PageIndex = 1, PageSize = 10 });

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SendMessageAsync_AdminSender_Success()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x", Role = UserRole.CLIENT };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x", Role = UserRole.EXPERT };
        var admin = new User { Id = adminId, FullName = "Admin User", Email = "admin@t.com", PasswordHash = "x", Role = UserRole.ADMIN };
        var conversation = new Conversation { Id = conversationId, ClientId = clientId, ExpertId = expertId, ProjectId = projectId };

        dbContext.Users.AddRange(client, expert, admin);
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);
        var request = new Request.SendMessageRequest
        {
            ConversationId = conversationId,
            Content = "Hello from Admin"
        };

        // Act
        var result = await service.SendMessageAsync(adminId, UserRole.ADMIN, request);

        // Assert
        result.Content.Should().Be("Hello from Admin");
        result.SenderId.Should().Be(adminId);
        result.SenderRole.Should().Be(UserRole.ADMIN);

        var msgInDb = await dbContext.Messages.FirstOrDefaultAsync(m => m.ConversationId == conversationId);
        msgInDb.Should().NotBeNull();
        msgInDb!.Content.Should().Be("Hello from Admin");
    }

    [Fact]
    public async Task GetConversationMessagesAsync_AdminSender_WithJobContext_Success()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x", Role = UserRole.CLIENT };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x", Role = UserRole.EXPERT };
        var admin = new User { Id = adminId, FullName = "Admin User", Email = "admin@t.com", PasswordHash = "x", Role = UserRole.ADMIN };
        var conversation = new Conversation { Id = conversationId, ClientId = clientId, ExpertId = expertId, JobId = jobId };
        var msg = new Message { ConversationId = conversationId, SenderId = clientId, Content = "Hello Job Context Message" };

        dbContext.Users.AddRange(client, expert, admin);
        dbContext.Conversations.Add(conversation);
        dbContext.Messages.Add(msg);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);

        // Act
        var result = await service.GetConversationMessagesAsync(adminId, UserRole.ADMIN, conversationId, new Aivora.Services.Base.Request.PageRequest { PageIndex = 1, PageSize = 10 });

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Content.Should().Be("Hello Job Context Message");
    }

    [Fact]
    public async Task GetConversationMessagesAsync_AdminSender_WithProjectContext_Success()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x", Role = UserRole.CLIENT };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x", Role = UserRole.EXPERT };
        var admin = new User { Id = adminId, FullName = "Admin User", Email = "admin@t.com", PasswordHash = "x", Role = UserRole.ADMIN };
        var conversation = new Conversation { Id = conversationId, ClientId = clientId, ExpertId = expertId, ProjectId = projectId };
        var msg = new Message { ConversationId = conversationId, SenderId = clientId, Content = "Hello Project Context Message" };

        dbContext.Users.AddRange(client, expert, admin);
        dbContext.Conversations.Add(conversation);
        dbContext.Messages.Add(msg);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);

        // Act
        var result = await service.GetConversationMessagesAsync(adminId, UserRole.ADMIN, conversationId, new Aivora.Services.Base.Request.PageRequest { PageIndex = 1, PageSize = 10 });

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items.First().Content.Should().Be("Hello Project Context Message");
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsCorrectSenderRole()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x", Role = UserRole.CLIENT };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x", Role = UserRole.EXPERT };
        var admin = new User { Id = adminId, FullName = "Admin User", Email = "admin@t.com", PasswordHash = "x", Role = UserRole.ADMIN };
        var conversation = new Conversation { Id = conversationId, ClientId = clientId, ExpertId = expertId, ProjectId = projectId };

        dbContext.Users.AddRange(client, expert, admin);
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);

        // Act & Assert 1: Client
        var clientRequest = new Request.SendMessageRequest { ConversationId = conversationId, Content = "Client msg" };
        var clientResult = await service.SendMessageAsync(clientId, UserRole.CLIENT, clientRequest);
        clientResult.SenderRole.Should().Be(UserRole.CLIENT);

        // Act & Assert 2: Expert
        var expertRequest = new Request.SendMessageRequest { ConversationId = conversationId, Content = "Expert msg" };
        var expertResult = await service.SendMessageAsync(expertId, UserRole.EXPERT, expertRequest);
        expertResult.SenderRole.Should().Be(UserRole.EXPERT);

        // Act & Assert 3: Admin
        var adminRequest = new Request.SendMessageRequest { ConversationId = conversationId, Content = "Admin msg" };
        var adminResult = await service.SendMessageAsync(adminId, UserRole.ADMIN, adminRequest);
        adminResult.SenderRole.Should().Be(UserRole.ADMIN);
    }

    [Fact]
    public async Task GetAdminConversationsAsync_FiltersAndPaging_ReturnsCorrectConversations()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x", Role = UserRole.CLIENT };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x", Role = UserRole.EXPERT };
        var otherUser = new User { Id = otherUserId, FullName = "Other", Email = "o@t.com", PasswordHash = "x", Role = UserRole.CLIENT };

        var conv1 = new Conversation { Id = Guid.NewGuid(), ClientId = clientId, ExpertId = expertId, JobId = jobId, UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-5) };
        var conv2 = new Conversation { Id = Guid.NewGuid(), ClientId = clientId, ExpertId = expertId, ProjectId = projectId, UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10) };
        var conv3 = new Conversation { Id = Guid.NewGuid(), ClientId = otherUserId, ExpertId = expertId, UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-15) };

        dbContext.Users.AddRange(client, expert, otherUser);
        dbContext.Conversations.AddRange(conv1, conv2, conv3);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);
        var pageRequest = new Aivora.Services.Base.Request.PageRequest { PageIndex = 1, PageSize = 10 };

        // Act - No filters
        var resultAll = await service.GetAdminConversationsAsync(null, null, null, pageRequest);
        // Act - Filter by JobId
        var resultJob = await service.GetAdminConversationsAsync(jobId, null, null, pageRequest);
        // Act - Filter by ProjectId
        var resultProject = await service.GetAdminConversationsAsync(null, projectId, null, pageRequest);
        // Act - Filter by UserId
        var resultUser = await service.GetAdminConversationsAsync(null, null, otherUserId, pageRequest);

        // Assert
        resultAll.TotalItems.Should().Be(3);
        resultJob.TotalItems.Should().Be(1);
        resultJob.Items.First().Id.Should().Be(conv1.Id);
        resultProject.TotalItems.Should().Be(1);
        resultProject.Items.First().Id.Should().Be(conv2.Id);
        resultUser.TotalItems.Should().Be(1);
        resultUser.Items.First().Id.Should().Be(conv3.Id);
    }

    [Fact]
    public async Task EnsureConversationParticipantAsync_AdminWithNoContext_ThrowsForbidden()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x", Role = UserRole.CLIENT };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x", Role = UserRole.EXPERT };
        var admin = new User { Id = adminId, FullName = "Admin", Email = "admin@t.com", PasswordHash = "x", Role = UserRole.ADMIN };
        var conversation = new Conversation { Id = conversationId, ClientId = clientId, ExpertId = expertId }; // No Job or Project context

        dbContext.Users.AddRange(client, expert, admin);
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);

        // Act
        Func<Task> act = async () => await service.EnsureConversationParticipantAsync(adminId, UserRole.ADMIN, conversationId);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task MarkAsReadAsync_AdminNotParticipant_ThrowsForbidden()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x", Role = UserRole.CLIENT };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x", Role = UserRole.EXPERT };
        var admin = new User { Id = adminId, FullName = "Admin", Email = "admin@t.com", PasswordHash = "x", Role = UserRole.ADMIN };

        // Conversation with Project context and open dispute (which would let admin view it, but NOT mark as read)
        var conversation = new Conversation { Id = conversationId, ClientId = clientId, ExpertId = expertId, ProjectId = projectId };
        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            OpenedBy = clientId,
            AgainstUserId = expertId,
            Status = DisputeStatus.OPEN,
            Reason = "Test Dispute"
        };

        dbContext.Users.AddRange(client, expert, admin);
        dbContext.Conversations.Add(conversation);
        dbContext.Disputes.Add(dispute);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);

        // Act
        Func<Task> act = async () => await service.MarkAsReadAsync(adminId, conversationId);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task EnsureConversationParticipantAsync_AdminWithOpenDispute_Succeeds()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x", Role = UserRole.CLIENT };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x", Role = UserRole.EXPERT };
        var admin = new User { Id = adminId, FullName = "Admin", Email = "admin@t.com", PasswordHash = "x", Role = UserRole.ADMIN };

        var conversation = new Conversation { Id = conversationId, ClientId = clientId, ExpertId = expertId, ProjectId = projectId };
        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            OpenedBy = clientId,
            AgainstUserId = expertId,
            Status = DisputeStatus.OPEN,
            Reason = "Test Dispute"
        };

        dbContext.Users.AddRange(client, expert, admin);
        dbContext.Conversations.Add(conversation);
        dbContext.Disputes.Add(dispute);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);

        // Act
        Func<Task> act = async () => await service.EnsureConversationParticipantAsync(adminId, UserRole.ADMIN, conversationId);

        // Assert
        await act.Should().NotThrowAsync();
    }
}



