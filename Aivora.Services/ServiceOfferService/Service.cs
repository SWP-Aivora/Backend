using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.ServiceOfferService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly NotificationService.IService _notificationService;

    public Service(AivoraDbContext dbContext, NotificationService.IService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    public async Task<Response.ServiceOfferResponse> CreateOfferAsync(Guid expertId, Guid serviceRequestId, Request.CreateServiceOfferRequest request)
    {
        if (request is null) throw new ValidationException("Request body is required.");
        if (request.Amount <= 0) throw new ValidationException("Offer amount must be greater than 0.");
        if (request.Milestones is null || request.Milestones.Count == 0) throw new ValidationException("At least one milestone is required.");

        foreach (var milestone in request.Milestones)
        {
            if (milestone.Amount <= 0) throw new ValidationException("Milestone amounts must be greater than 0.");
            if (milestone.DueDays < 1 || milestone.DueDays > 3650) throw new ValidationException("Milestone due days must be between 1 and 3650.");
        }

        var serviceRequest = await _dbContext.ServiceRequests
            .Include(r => r.Service)
            .FirstOrDefaultAsync(r => r.Id == serviceRequestId);

        if (serviceRequest == null) throw new NotFoundException("Service request not found.");
        if (serviceRequest.Service.ExpertId != expertId) throw new ForbiddenException("Only the service owner can send an offer for this request.");
        if (serviceRequest.Status != ServiceRequestStatus.ACCEPTED) throw new ValidationException("Service request must be accepted before sending an offer.");

        var offer = new ServiceOffer
        {
            ServiceRequestId = serviceRequestId,
            ExpertId = expertId,
            Amount = request.Amount,
            Status = ServiceOfferStatus.PENDING,
            Milestones = request.Milestones.Select(m => new ServiceOfferMilestone
            {
                Title = m.Title,
                Description = m.Description,
                Amount = m.Amount,
                DueDays = m.DueDays,
                AcceptanceCriteria = m.AcceptanceCriteria,
                OrderIndex = m.OrderIndex
            }).ToList()
        };

        _dbContext.ServiceOffers.Add(offer);
        await _dbContext.SaveChangesAsync();

        try
        {
            await _notificationService.SendNotificationAsync(
                serviceRequest.ClientId,
                "New offer received",
                $"The expert sent you an offer for \"{serviceRequest.Service.Title}\".",
                "SERVICE_OFFER",
                $"/service-requests/{serviceRequestId}"
            );
        }
        catch
        {
            // Notification failure should not block the main business flow
        }

        return MapToResponse(offer);
    }

    public async Task<Response.AcceptServiceOfferResultResponse> AcceptOfferAsync(Guid clientId, Guid serviceOfferId)
    {
        var offer = await _dbContext.ServiceOffers
            .Include(o => o.ServiceRequest).ThenInclude(r => r.Service)
            .Include(o => o.Milestones)
            .FirstOrDefaultAsync(o => o.Id == serviceOfferId);

        if (offer == null) throw new NotFoundException("Service offer not found.");
        if (offer.ServiceRequest.ClientId != clientId) throw new ForbiddenException("Only the client who requested the service can accept this offer.");
        if (offer.Status != ServiceOfferStatus.PENDING) throw new ValidationException("Only pending offers can be accepted.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        Project project;
        try
        {
            offer.Status = ServiceOfferStatus.ACCEPTED;

            project = HiringService.ProjectFactory.CreateProjectWithMilestones(
                clientId: offer.ServiceRequest.ClientId,
                expertId: offer.ExpertId,
                title: offer.ServiceRequest.Service.Title,
                description: offer.ServiceRequest.Service.Description,
                budget: offer.Amount,
                currency: "AICOIN",
                jobId: null,
                acceptedProposalId: null,
                serviceRequestId: offer.ServiceRequestId,
                milestones: offer.Milestones.Select(m => (m.Title, m.Description, m.Amount, m.OrderIndex)));

            _dbContext.Projects.Add(project);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Race condition: another concurrent accept for the same ServiceRequest already
            // committed a Project between our PENDING check and SaveChangesAsync. The partial
            // unique index on Projects.ServiceRequestId caught it.
            await transaction.RollbackAsync();
            throw new ValidationException("This service request already has an accepted offer.");
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        try
        {
            await _notificationService.SendNotificationAsync(
                offer.ExpertId,
                "Offer accepted",
                $"Your offer for \"{offer.ServiceRequest.Service.Title}\" was accepted.",
                "SERVICE_OFFER",
                $"/projects/{project.Id}"
            );
        }
        catch
        {
            // Notification failure should not block the main business flow
        }

        return new Response.AcceptServiceOfferResultResponse
        {
            ProjectId = project.Id,
            ServiceOfferId = offer.Id,
            Status = offer.Status.ToString()
        };
    }

    public async Task<Response.ServiceOfferResponse> GetOfferForServiceRequestAsync(Guid clientId, Guid serviceRequestId)
    {
        var serviceRequest = await _dbContext.ServiceRequests
            .FirstOrDefaultAsync(r => r.Id == serviceRequestId);

        if (serviceRequest == null) throw new NotFoundException("Service request not found.");
        if (serviceRequest.ClientId != clientId) throw new ForbiddenException("Only the client who owns this request can view its offer.");

        var offers = await _dbContext.ServiceOffers
            .Include(o => o.Milestones)
            .Where(o => o.ServiceRequestId == serviceRequestId)
            .ToListAsync();

        var offer = offers
            .OrderByDescending(o => o.Status == ServiceOfferStatus.ACCEPTED)
            .ThenByDescending(o => o.CreatedAt)
            .FirstOrDefault();

        if (offer == null) throw new NotFoundException("No offer found for this service request.");

        return MapToResponse(offer);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException?.Message?.Contains("23505") == true;
    }

    private static Response.ServiceOfferResponse MapToResponse(ServiceOffer offer)
    {
        return new Response.ServiceOfferResponse
        {
            Id = offer.Id,
            ServiceRequestId = offer.ServiceRequestId,
            ExpertId = offer.ExpertId,
            Amount = offer.Amount,
            Status = offer.Status,
            CreatedAt = offer.CreatedAt,
            Milestones = offer.Milestones.Select(m => new Response.ServiceOfferMilestoneResponse
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                Amount = m.Amount,
                DueDays = m.DueDays,
                AcceptanceCriteria = m.AcceptanceCriteria,
                OrderIndex = m.OrderIndex
            }).ToList()
        };
    }
}
