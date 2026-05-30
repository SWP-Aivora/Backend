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

        var msg1 = new Message { ConversationId = conversationId, SenderId = expertId, Content = "Msg from expert", IsRead = false };
        var msg2 = new Message { ConversationId = conversationId, SenderId = clientId, Content = "Msg from client", IsRead = false };

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
}
