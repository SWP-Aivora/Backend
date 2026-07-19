using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Services.Base;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aivora.Services.NotificationService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly ILogger<Service>? _logger;
    private readonly IServiceScopeFactory? _scopeFactory;

    public Service(AivoraDbContext dbContext, ILogger<Service>? logger = null, IServiceScopeFactory? scopeFactory = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    // Fire-and-forget gui reliable, nen. Chuyen tu Treasury sang day vi day moi
    // la module so huu "cach giao notification". scopeFactory optional vi
    // NotificationServiceTests dung new Service(dbContext) truc tiep.
    public void SendInBackground(Guid userId, string title, string message, string? type, string? linkUrl)
    {
        if (_scopeFactory == null)
        {
            // Khong co scopeFactory (vd test): khong dung this._dbContext trong
            // Task.Run - scope cua request co the da dispose truoc khi task chay,
            // race disposal. Bo qua, chi log neu co logger.
            _logger?.LogWarning("scopeFactory chua cau hinh, bo qua background notification cho user {UserId}.", userId);
            return;
        }

        _ = Task.Run(async () =>
        {
            const int maxRetries = 3;
            int delayMs = 1000;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var notificationService = scope.ServiceProvider.GetRequiredService<IService>();
                        await notificationService.SendNotificationAsync(userId, title, message, type, linkUrl);
                    }
                    return; // Success, exit
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Attempt {Attempt} failed to send notification in background to user {UserId}. Title: {Title}", i + 1, userId, title);
                    if (i < maxRetries - 1)
                    {
                        await Task.Delay(delayMs);
                        delayMs *= 2; // exponential backoff
                    }
                    else
                    {
                        _logger?.LogError(ex, "All attempts failed to send notification to user {UserId}. Title: {Title}", userId, title);
                    }
                }
            }
        });
    }

    public async Task<Response.NotificationResponse> SendNotificationAsync(Guid userId, string title, string message, string? type = null, string? linkUrl = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            LinkUrl = linkUrl,
            IsRead = false
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync();

        return MapToResponse(notification);
    }

    public async Task<Aivora.Services.Base.Response.PageResult<Response.NotificationResponse>> GetUserNotificationsAsync(Guid userId, Aivora.Services.Base.Request.PageRequest pageRequest)
    {
        var query = _dbContext.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt);

        var totalItems = await query.CountAsync();
        var items = await query
            .Skip((pageRequest.PageIndex - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .ToListAsync();

        return new Aivora.Services.Base.Response.PageResult<Response.NotificationResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalItems = totalItems,
            PageIndex = pageRequest.PageIndex,
            PageSize = pageRequest.PageSize
        };
    }

    public async Task<bool> MarkAsReadAsync(Guid userId, Guid notificationId)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null) throw new NotFoundException("Notification not found.");

        notification.IsRead = true;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkAllAsReadAsync(Guid userId)
    {
        var notifications = await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in notifications)
        {
            n.IsRead = true;
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        return await _dbContext.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    private static Response.NotificationResponse MapToResponse(Notification n)
    {
        return new Response.NotificationResponse
        {
            Id = n.Id,
            Title = n.Title,
            Message = n.Message,
            LinkUrl = n.LinkUrl,
            IsRead = n.IsRead,
            Type = n.Type,
            CreatedAt = n.CreatedAt
        };
    }
}
