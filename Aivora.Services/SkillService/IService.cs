namespace Aivora.Services.SkillService;

public interface IService
{
    Task<List<Response.SkillResponse>> GetSkillsAsync(Guid? categoryId = null);
    Task<Response.SkillResponse> GetSkillByIdAsync(Guid id);
    Task<Response.SkillResponse> CreateSkillAsync(Request.CreateSkillRequest request);

    Task<Response.ExpertSkillResponse> AddExpertSkillAsync(Guid userId, Request.AddExpertSkillRequest request);
    Task<bool> RemoveExpertSkillAsync(Guid userId, Guid skillId);
}
