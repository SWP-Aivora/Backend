using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.ServiceCatalogService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;

    public Service(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Response.ServiceResponse> CreateServiceAsync(Guid expertId, Request.CreateServiceRequest request)
    {
        if (request is null) throw new ValidationException("Request body is required.");
        if (string.IsNullOrWhiteSpace(request.Title)) throw new ValidationException("Title is required.");
        if (string.IsNullOrWhiteSpace(request.Description)) throw new ValidationException("Description is required.");

        var packages = NormalizePackages(request.Packages);
        if (packages.Count == 0) throw new ValidationException("At least one package is required.");
        var faqs = NormalizeFaqs(request.Faqs);
        if (faqs.Count == 0) throw new ValidationException("At least one FAQ is required.");

        var service = new ServiceListing
        {
            ExpertId = expertId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            AttachmentUrl = request.AttachmentUrl,
            Status = ServiceStatus.DRAFT,
            Packages = packages.Select(p => new ServicePackage
            {
                Tier = p.Tier,
                Title = p.Title,
                Description = p.Description,
                Price = p.Price,
                DeliveryDays = p.DeliveryDays,
                Features = p.Features
            }).ToList(),
            Faqs = faqs.Select(f => new ServiceFaq
            {
                Question = f.Question,
                Answer = f.Answer
            }).ToList()
        };

        _dbContext.Services.Add(service);
        await _dbContext.SaveChangesAsync();

        return await GetServiceByIdAsync(service.Id, expertId);
    }

    public async Task<Response.ServiceResponse> UpdateServiceAsync(Guid expertId, Guid serviceId, Request.UpdateServiceRequest request)
    {
        var service = await _dbContext.Services
            .FirstOrDefaultAsync(s => s.Id == serviceId && s.ExpertId == expertId);

        if (service == null) throw new NotFoundException("Service not found or access denied.");

        if (request.Title != null)
        {
            if (string.IsNullOrWhiteSpace(request.Title)) throw new ValidationException("Title is required.");
            service.Title = request.Title.Trim();
        }
        if (request.Description != null)
        {
            if (string.IsNullOrWhiteSpace(request.Description)) throw new ValidationException("Description is required.");
            service.Description = request.Description.Trim();
        }
        if (request.AttachmentUrl != null) service.AttachmentUrl = request.AttachmentUrl;

        if (request.Packages != null)
        {
            var normalizedPackages = NormalizePackages(request.Packages);
            if (normalizedPackages.Count == 0) throw new ValidationException("At least one package is required.");
            var oldPackages = await _dbContext.ServicePackages.Where(p => p.ServiceId == serviceId).ToListAsync();
            _dbContext.ServicePackages.RemoveRange(oldPackages);
            var newPackages = normalizedPackages.Select(p => new ServicePackage
            {
                ServiceId = serviceId,
                Tier = p.Tier,
                Title = p.Title,
                Description = p.Description,
                Price = p.Price,
                DeliveryDays = p.DeliveryDays,
                Features = p.Features
            }).ToList();
            await _dbContext.ServicePackages.AddRangeAsync(newPackages);
            foreach (var old in oldPackages) service.Packages.Remove(old);
        }

        if (request.Faqs != null)
        {
            var normalizedFaqs = NormalizeFaqs(request.Faqs);
            if (normalizedFaqs.Count == 0) throw new ValidationException("At least one FAQ is required.");
            var oldFaqs = await _dbContext.ServiceFaqs.Where(f => f.ServiceId == serviceId).ToListAsync();
            _dbContext.ServiceFaqs.RemoveRange(oldFaqs);
            var newFaqs = normalizedFaqs.Select(f => new ServiceFaq
            {
                ServiceId = serviceId,
                Question = f.Question,
                Answer = f.Answer
            }).ToList();
            await _dbContext.ServiceFaqs.AddRangeAsync(newFaqs);
            foreach (var old in oldFaqs) service.Faqs.Remove(old);
        }

        await _dbContext.SaveChangesAsync();

        return await GetServiceByIdAsync(service.Id, expertId);
    }

    public async Task<Response.ServiceResponse> PublishServiceAsync(Guid expertId, Guid serviceId)
    {
        var service = await _dbContext.Services.FirstOrDefaultAsync(s => s.Id == serviceId && s.ExpertId == expertId);
        if (service == null) throw new NotFoundException("Service not found or access denied.");
        if (service.Status == ServiceStatus.PUBLISHED) throw new ValidationException("Service is already published.");

        var hasPackage = await _dbContext.ServicePackages.AnyAsync(p => p.ServiceId == serviceId);
        if (!hasPackage) throw new ValidationException("Service must have at least one package before publishing.");
        var hasFaq = await _dbContext.ServiceFaqs.AnyAsync(f => f.ServiceId == serviceId);
        if (!hasFaq) throw new ValidationException("Service must have at least one FAQ before publishing.");

        service.Status = ServiceStatus.PUBLISHED;
        service.PublishedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync();

        return await GetServiceByIdAsync(service.Id, expertId);
    }

    public async Task<Response.ServiceResponse> UnpublishServiceAsync(Guid expertId, Guid serviceId)
    {
        var service = await _dbContext.Services.FirstOrDefaultAsync(s => s.Id == serviceId && s.ExpertId == expertId);
        if (service == null) throw new NotFoundException("Service not found or access denied.");
        if (service.Status != ServiceStatus.PUBLISHED) throw new ValidationException("Service is not published.");

        service.Status = ServiceStatus.DRAFT;
        await _dbContext.SaveChangesAsync();

        return await GetServiceByIdAsync(service.Id, expertId);
    }

    public async Task<List<Response.ServiceResponse>> GetMyServicesAsync(Guid expertId)
    {
        var services = await _dbContext.Services
            .Include(s => s.Expert)
            .Include(s => s.Packages)
            .Include(s => s.Faqs)
            .Where(s => s.ExpertId == expertId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return services.Select(MapToResponse).ToList();
    }

    public async Task<Response.ServiceResponse> GetServiceByIdAsync(Guid serviceId, Guid? viewerId)
    {
        var service = await _dbContext.Services
            .Include(s => s.Expert)
            .Include(s => s.Packages)
            .Include(s => s.Faqs)
            .FirstOrDefaultAsync(s => s.Id == serviceId);

        if (service == null) throw new NotFoundException("Service not found.");
        if (service.Status != ServiceStatus.PUBLISHED && service.ExpertId != viewerId)
            throw new NotFoundException("Service not found.");

        return MapToResponse(service);
    }

    public async Task<Base.Response.PageResult<Response.ServiceResponse>> GetPublishedServicesAsync(Base.Request.PageRequest pageRequest)
    {
        var query = _dbContext.Services
            .Include(s => s.Expert)
            .Include(s => s.Packages)
            .Include(s => s.Faqs)
            .Where(s => s.Status == ServiceStatus.PUBLISHED);

        if (!string.IsNullOrWhiteSpace(pageRequest.SearchTerm))
        {
            query = query.Where(s =>
                s.Title.Contains(pageRequest.SearchTerm) ||
                s.Description.Contains(pageRequest.SearchTerm));
        }

        var totalItems = await query.CountAsync();

        var services = await query
            .OrderByDescending(s => s.PublishedAt)
            .ThenByDescending(s => s.Id)
            .Skip((pageRequest.PageIndex - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .ToListAsync();

        return new Base.Response.PageResult<Response.ServiceResponse>
        {
            Items = services.Select(MapToResponse).ToList(),
            TotalItems = totalItems,
            PageIndex = pageRequest.PageIndex,
            PageSize = pageRequest.PageSize
        };
    }

    private static Response.ServiceResponse MapToResponse(ServiceListing service)
    {
        return new Response.ServiceResponse
        {
            Id = service.Id,
            ExpertId = service.ExpertId,
            ExpertName = service.Expert?.FullName ?? "N/A",
            Title = service.Title,
            Description = service.Description,
            Status = service.Status,
            AttachmentUrl = service.AttachmentUrl,
            CreatedAt = service.CreatedAt,
            PublishedAt = service.PublishedAt,
            Packages = service.Packages.Select(p => new Response.PackageResponse
            {
                Id = p.Id,
                Tier = p.Tier,
                Title = p.Title,
                Description = p.Description,
                Price = p.Price,
                DeliveryDays = p.DeliveryDays,
                Features = p.Features
            }).ToList(),
            Faqs = service.Faqs.Select(f => new Response.FaqResponse
            {
                Id = f.Id,
                Question = f.Question,
                Answer = f.Answer
            }).ToList()
        };
    }

    private static List<Request.PackageRequest> NormalizePackages(IEnumerable<Request.PackageRequest>? packages)
    {
        var normalized = (packages ?? Enumerable.Empty<Request.PackageRequest>())
            .Select(p => new Request.PackageRequest
            {
                Tier = p.Tier,
                Title = string.IsNullOrWhiteSpace(p.Title) ? p.Tier.ToString() : p.Title.Trim(),
                Description = p.Description,
                Price = p.Price,
                DeliveryDays = p.DeliveryDays,
                Features = p.Features
            })
            .ToList();

        foreach (var package in normalized)
        {
            if (package.Price <= 0) throw new ValidationException("Package price must be greater than 0.");
            if (package.DeliveryDays < 1 || package.DeliveryDays > 3650)
                throw new ValidationException("Package delivery days must be between 1 and 3650.");
        }

        return normalized;
    }

    private static List<Request.FaqRequest> NormalizeFaqs(IEnumerable<Request.FaqRequest>? faqs)
    {
        var normalized = (faqs ?? Enumerable.Empty<Request.FaqRequest>())
            .Select(f => new Request.FaqRequest
            {
                Question = f.Question?.Trim() ?? string.Empty,
                Answer = f.Answer?.Trim() ?? string.Empty
            })
            .ToList();

        foreach (var faq in normalized)
        {
            if (string.IsNullOrWhiteSpace(faq.Question)) throw new ValidationException("FAQ question is required.");
            if (string.IsNullOrWhiteSpace(faq.Answer)) throw new ValidationException("FAQ answer is required.");
        }

        return normalized;
    }
}
