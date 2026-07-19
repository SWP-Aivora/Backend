using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.Base;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.ProjectService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;

    public Service(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Response.ProjectResponse> GetProjectByIdAsync(Guid userId, Guid projectId, UserRole userRole)
    {
        var project = await _dbContext.Projects
            .Include(p => p.Client)
            .Include(p => p.Expert)
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null) throw new NotFoundException("Project not found.");

        // Updated authorization logic
        // Admin always has access
        // Client can only access their own projects
        // Expert can only access assigned projects
        if (userRole == UserRole.ADMIN)
        {
            return MapToResponse(project);
        }

        if (userRole == UserRole.CLIENT && project.ClientId == userId)
        {
            return MapToResponse(project);
        }

        if (userRole == UserRole.EXPERT && project.ExpertId == userId)
        {
            return MapToResponse(project);
        }

        throw new ForbiddenException("Access denied to this project.");
    }

    public async Task<Aivora.Services.Base.Response.PageResult<Response.ProjectResponse>> GetProjectsAsync(Guid userId, UserRole role, Aivora.Services.Base.Request.PageRequest pageRequest, ProjectStatus? status = null)
    {
        var query = _dbContext.Projects
            .Include(p => p.Client)
            .Include(p => p.Expert)
            .AsQueryable();

        if (role == UserRole.CLIENT)
        {
            query = query.Where(p => p.ClientId == userId);
        }
        else if (role == UserRole.EXPERT)
        {
            query = query.Where(p => p.ExpertId == userId);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(pageRequest.SearchTerm))
        {
            query = query.Where(p => p.Title.Contains(pageRequest.SearchTerm));
        }

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageRequest.PageIndex - 1) * pageRequest.PageSize)
            .Take(pageRequest.PageSize)
            .ToListAsync();

        return new Aivora.Services.Base.Response.PageResult<Response.ProjectResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalItems = totalItems,
            PageIndex = pageRequest.PageIndex,
            PageSize = pageRequest.PageSize
        };
    }

    public async Task<Response.ProjectResponse> CancelProjectAsync(Guid userId, Guid projectId, string? reason)
    {
        var project = await _dbContext.Projects
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == projectId && p.ClientId == userId);

        if (project == null) throw new NotFoundException("Project not found or access denied.");

        // Business Rule: Allowed only if no milestone has payment status HELD, FROZEN, or RELEASED
        // In our current simple model, we check if any milestone is funded.
        if (project.Milestones.Any(m => m.Status != MilestoneStatus.CREATED))
            throw new ValidationException("Cannot cancel project after milestones have been funded or processed.");

        project.Status = ProjectStatus.CANCELLED;
        // Optionally store reason

        await _dbContext.SaveChangesAsync();
        return MapToResponse(project);
    }

    private static Response.ProjectResponse MapToResponse(Project p)
    {
        return new Response.ProjectResponse
        {
            Id = p.Id,
            JobId = p.JobId,
            AcceptedProposalId = p.AcceptedProposalId,
            ServiceRequestId = p.ServiceRequestId,
            ClientId = p.ClientId,
            ClientName = p.Client?.FullName ?? "N/A",
            ExpertId = p.ExpertId,
            ExpertName = p.Expert?.FullName ?? "N/A",
            Title = p.Title,
            Description = p.Description,
            TotalBudget = p.TotalBudget ?? 0,
            Currency = p.Currency,
            Status = p.Status,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            CompletedAt = p.CompletedAt,
            CreatedAt = p.CreatedAt,
            Milestones = p.Milestones.Select(m => new Response.MilestoneInfo
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                Amount = m.Amount,
                Currency = m.Currency,
                Status = m.Status,
                OrderIndex = m.OrderIndex,
                DueDate = m.DueDate
            }).OrderBy(m => m.OrderIndex).ToList()
        };
    }
}
