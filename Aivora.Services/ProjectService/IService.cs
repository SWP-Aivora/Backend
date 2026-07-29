using Aivora.Repositories.Enums;
using Aivora.Services.Base;

namespace Aivora.Services.ProjectService;

public interface IService
{
    Task<Response.ProjectResponse> GetProjectByIdAsync(Guid userId, Guid projectId, UserRole userRole);
    Task<Aivora.Services.Base.Response.PageResult<Response.ProjectResponse>> GetProjectsAsync(Guid userId, UserRole role, Aivora.Services.Base.Request.PageRequest pageRequest, ProjectStatus? status = null);
    Task<Response.ProjectResponse> CancelProjectAsync(Guid userId, Guid projectId, string? reason);
    Task<Response.ProjectResponse> CancelDisputedProjectAsync(Guid userId, Guid projectId);
}
