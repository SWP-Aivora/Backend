using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.ServiceRequestService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly NotificationService.IService _notificationService;
    private readonly MessageService.IService _messageService;

    public Service(AivoraDbContext dbContext, NotificationService.IService notificationService, MessageService.IService messageService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _messageService = messageService;
    }

    public async Task<Response.ServiceRequestResponse> CreateRequestAsync(Guid clientId, Guid serviceId, Request.CreateServiceRequestRequest request)
    {
        if (request is null) throw new ValidationException("Request body is required.");

        var service = await _dbContext.Services.FirstOrDefaultAsync(s => s.Id == serviceId);
        if (service == null) throw new NotFoundException("Service not found.");
        if (service.Status != ServiceStatus.PUBLISHED) throw new ValidationException("Service is not published.");
        if (service.ExpertId == clientId) throw new ValidationException("You cannot request your own service.");

        var package = await _dbContext.ServicePackages.FirstOrDefaultAsync(p => p.Id == request.PackageId && p.ServiceId == serviceId);
        if (package == null) throw new NotFoundException("Package not found for this service.");

        var hasPendingRequest = await _dbContext.ServiceRequests
            .AnyAsync(r => r.ServiceId == serviceId && r.ClientId == clientId && r.Status == ServiceRequestStatus.PENDING);
        if (hasPendingRequest) throw new ValidationException("You already have a pending request for this service.");

        var serviceRequest = new ServiceRequest
        {
            ServiceId = serviceId,
            ClientId = clientId,
            PackageId = package.Id,
            Note = request.Note,
            Status = ServiceRequestStatus.PENDING,
            PackageTitle = package.Title,
            PackagePrice = package.Price,
            PackageDeliveryDays = package.DeliveryDays
        };

        _dbContext.ServiceRequests.Add(serviceRequest);
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Race condition: another concurrent request from this client for this service
            // committed between our AnyAsync check and SaveChangesAsync. The partial unique
            // index on (ServiceId, ClientId) WHERE Status = 'PENDING' caught it.
            throw new ValidationException("You already have a pending request for this service.");
        }

        try
        {
            await _notificationService.SendNotificationAsync(
                service.ExpertId,
                "New service request",
                $"A client has requested your service \"{service.Title}\".",
                "SERVICE_REQUEST",
                $"/services/{service.Id}/requests"
            );
        }
        catch
        {
            // Notification failure should not block the main business flow
        }

        return await GetRequestByIdAsync(serviceRequest.Id);
    }

    public async Task<List<Response.ServiceRequestResponse>> GetRequestsByServiceAsync(Guid expertId, Guid serviceId)
    {
        var service = await _dbContext.Services.FirstOrDefaultAsync(s => s.Id == serviceId);
        if (service == null) throw new NotFoundException("Service not found.");
        if (service.ExpertId != expertId) throw new ForbiddenException("Only the service owner can view its requests.");

        var requests = await _dbContext.ServiceRequests
            .Include(r => r.Service)
            .Include(r => r.Client)
            .Where(r => r.ServiceId == serviceId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return requests.Select(MapToResponse).ToList();
    }

    public async Task<List<Response.ServiceRequestResponse>> GetMyRequestsForExpertAsync(Guid expertId, ServiceRequestStatus? status)
    {
        var query = _dbContext.ServiceRequests
            .Include(r => r.Service)
            .Include(r => r.Client)
            .Where(r => r.Service.ExpertId == expertId);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var requests = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

        return requests.Select(MapToResponse).ToList();
    }

    public async Task<Response.ServiceRequestResponse> AcceptRequestAsync(Guid expertId, Guid serviceRequestId)
    {
        var request = await LoadOwnedPendingRequestAsync(expertId, serviceRequestId, "accepted");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            request.Status = ServiceRequestStatus.ACCEPTED;
            await _dbContext.SaveChangesAsync();

            await _messageService.GetOrCreateConversationAsync(request.ClientId, expertId, serviceRequestId: request.Id);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        try
        {
            await _notificationService.SendNotificationAsync(
                request.ClientId,
                "Service request accepted",
                $"Your request for \"{request.Service.Title}\" was accepted.",
                "SERVICE_REQUEST",
                $"/services/{request.ServiceId}/requests"
            );
        }
        catch
        {
            // Notification failure should not block the main business flow
        }

        return await GetRequestByIdAsync(request.Id);
    }

    public async Task<Response.ServiceRequestResponse> DeclineRequestAsync(Guid expertId, Guid serviceRequestId)
    {
        var request = await LoadOwnedPendingRequestAsync(expertId, serviceRequestId, "declined");

        request.Status = ServiceRequestStatus.DECLINED;
        await _dbContext.SaveChangesAsync();

        try
        {
            await _notificationService.SendNotificationAsync(
                request.ClientId,
                "Service request declined",
                $"Your request for \"{request.Service.Title}\" was declined.",
                "SERVICE_REQUEST",
                $"/services/{request.ServiceId}/requests"
            );
        }
        catch
        {
            // Notification failure should not block the main business flow
        }

        return await GetRequestByIdAsync(request.Id);
    }

    private async Task<ServiceRequest> LoadOwnedPendingRequestAsync(Guid expertId, Guid serviceRequestId, string action)
    {
        var request = await _dbContext.ServiceRequests
            .Include(r => r.Service)
            .FirstOrDefaultAsync(r => r.Id == serviceRequestId);

        if (request == null) throw new NotFoundException("Service request not found.");
        if (request.Service.ExpertId != expertId) throw new ForbiddenException("Only the service owner can respond to this request.");
        if (request.Status != ServiceRequestStatus.PENDING) throw new ValidationException($"Only pending requests can be {action}.");

        return request;
    }

    private async Task<Response.ServiceRequestResponse> GetRequestByIdAsync(Guid id)
    {
        var request = await _dbContext.ServiceRequests
            .Include(r => r.Service)
            .Include(r => r.Client)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null) throw new NotFoundException("Service request not found.");

        return MapToResponse(request);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException?.Message?.Contains("23505") == true;
    }

    private static Response.ServiceRequestResponse MapToResponse(ServiceRequest request)
    {
        return new Response.ServiceRequestResponse
        {
            Id = request.Id,
            ServiceId = request.ServiceId,
            ServiceTitle = request.Service?.Title ?? "N/A",
            ExpertId = request.Service?.ExpertId ?? Guid.Empty,
            ClientId = request.ClientId,
            ClientName = request.Client?.FullName ?? "N/A",
            PackageId = request.PackageId,
            PackageTitle = request.PackageTitle,
            PackagePrice = request.PackagePrice,
            PackageDeliveryDays = request.PackageDeliveryDays,
            Note = request.Note,
            Status = request.Status,
            CreatedAt = request.CreatedAt
        };
    }
}
