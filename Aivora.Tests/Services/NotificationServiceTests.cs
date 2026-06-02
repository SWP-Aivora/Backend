using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Services.NotificationService;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aivora.Tests.Services;

public class NotificationServiceTests
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
    public async Task SendNotificationAsync_CreatesNotificationSuccessfully()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var service = new Service(dbContext);

        // Act
        var result = await service.SendNotificationAsync(userId, "Test Title", "Test Message", "PROPOSAL", "/jobs/1");

        // Assert
        result.Title.Should().Be("Test Title");
        result.IsRead.Should().BeFalse();
        
        var count = await dbContext.Notifications.CountAsync(n => n.UserId == userId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task MarkAsReadAsync_UpdatesStatus()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var notification = new Notification { UserId = userId, Title = "T", Message = "M", IsRead = false };
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);

        // Act
        await service.MarkAsReadAsync(userId, notification.Id);

        // Assert
        var updated = await dbContext.Notifications.FindAsync(notification.Id);
        updated!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        dbContext.Notifications.AddRange(
            new Notification { UserId = userId, Title = "1", Message = "M", IsRead = false },
            new Notification { UserId = userId, Title = "2", Message = "M", IsRead = true },
            new Notification { UserId = userId, Title = "3", Message = "M", IsRead = false }
        );
        await dbContext.SaveChangesAsync();

        var service = new Service(dbContext);

        // Act
        var count = await service.GetUnreadCountAsync(userId);

        // Assert
        count.Should().Be(2);
    }
}
