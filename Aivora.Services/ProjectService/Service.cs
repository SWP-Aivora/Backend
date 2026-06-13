using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Repositories.Repositories.Projects;
using Aivora.Services.Base;
using Aivora.Services.Exceptions;

namespace Aivora.Services.ProjectService;

public class ProjectApplicationService : IService
{
    private readonly IProjectRepository _projectRepository;

    public ProjectApplicationService(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<Response.ProjectResponse> GetProjectByIdAsync(Guid userId, Guid projectId)
    {
        var project = await _projectRepository.GetDetailedByIdAsync(projectId);

        if (project == null) throw new NotFoundException("Project not found.");

        // Security check
        if (project.ClientId != userId && project.ExpertId != userId)
            throw new UnauthorizedException("Access denied to this project.");

        return MapToResponse(project);
    }

    public async Task<Aivora.Services.Base.Response.PageResult<Response.ProjectResponse>> GetProjectsAsync(Guid userId, UserRole role, Aivora.Services.Base.Request.PageRequest pageRequest, ProjectStatus? status = null)
    {
        var (items, totalItems) = await _projectRepository.ListForUserAsync(
            userId,
            role,
            pageRequest.PageIndex,
            pageRequest.PageSize,
            pageRequest.SearchTerm,
            status);

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
        var project = await _projectRepository.GetOwnedWithMilestonesAsync(projectId, userId);

        if (project == null) throw new NotFoundException("Project not found or access denied.");

        // Business Rule: Allowed only if no milestone has payment status HELD, FROZEN, or RELEASED
        // In our current simple model, we check if any milestone is funded.
        if (project.Milestones.Any(m => m.Status != MilestoneStatus.CREATED))
            throw new ValidationException("Cannot cancel project after milestones have been funded or processed.");

        project.Status = ProjectStatus.CANCELLED;
        // Optionally store reason

        await _projectRepository.SaveChangesAsync();
        return MapToResponse(project);
    }

    private static Response.ProjectResponse MapToResponse(Project p)
    {
        return new Response.ProjectResponse
        {
            Id = p.Id,
            JobId = p.JobId,
            AcceptedProposalId = p.AcceptedProposalId,
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
