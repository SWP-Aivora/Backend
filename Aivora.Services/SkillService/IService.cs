namespace Aivora.Services.SkillService;

public interface IService
{
    Task<List<Response.SkillResponse>> GetSkillsAsync(Guid? categoryId = null);
    Task<Response.SkillResponse> GetSkillByIdAsync(Guid id);
    Task<Response.SkillResponse> CreateSkillAsync(Request.CreateSkillRequest request);
}
