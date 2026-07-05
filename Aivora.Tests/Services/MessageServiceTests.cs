using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.MessageService;
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

        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x" };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x" };
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
        var result = await service.SendMessageAsync(clientId, request);

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
    public async Task MarkAsReadAsync_UpdatesOnlyIncomingMessages()
    {
        // Arrange
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x" };
        var expert = new User { Id = expertId, FullName = "Expert", Email = "e@t.com", PasswordHash = "x" };
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
        var client = new User { Id = clientId, FullName = "Client", Email = "c@t.com", PasswordHash = "x" };

        dbContext.Users.Add(client);
        dbContext.Conversations.Add(conversation);
        dbContext.Disputes.Add(dispute);
        dbContext.Messages.Add(msg);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);

        var result = await service.GetConversationMessagesAsync(adminId, conversationId, new Aivora.Services.Base.Request.PageRequest { PageIndex = 1, PageSize = 10 }, isAdmin: true);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetConversationMessagesAsync_Admin_NoDispute_ThrowsUnauthorized()
    {
        var dbContext = GetDbContext();
        var clientId = Guid.NewGuid();
        var expertId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var conversation = new Conversation { Id = conversationId, ClientId = clientId, ExpertId = expertId, ProjectId = projectId };
        
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);

        Func<Task> act = async () => await service.GetConversationMessagesAsync(adminId, conversationId, new Aivora.Services.Base.Request.PageRequest { PageIndex = 1, PageSize = 10 }, isAdmin: true);

        await act.Should().ThrowAsync<Aivora.Services.Exceptions.UnauthorizedException>();
    }

    [Fact]
    public async Task GetConversationMessagesAsync_Admin_ResolvedDispute_ThrowsUnauthorized()
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
            Status = DisputeStatus.RESOLVED,
            Reason = "Test"
        };

        dbContext.Conversations.Add(conversation);
        dbContext.Disputes.Add(dispute);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);

        Func<Task> act = async () => await service.GetConversationMessagesAsync(adminId, conversationId, new Aivora.Services.Base.Request.PageRequest { PageIndex = 1, PageSize = 10 }, isAdmin: true);

        await act.Should().ThrowAsync<Aivora.Services.Exceptions.UnauthorizedException>();
    }
}

