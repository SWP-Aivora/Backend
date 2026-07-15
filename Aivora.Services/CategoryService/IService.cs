namespace Aivora.Services.CategoryService;

public interface IService
{
    Task<List<Response.CategoryResponse>> GetCategoriesAsync();
    Task<Response.CategoryResponse> GetCategoryByIdAsync(Guid id);
    Task<Response.CategoryResponse> CreateCategoryAsync(Request.CreateCategoryRequest request);
    Task<Dictionary<Guid, string>> GetCachedCategoryDictionaryAsync(CancellationToken cancellationToken = default);
}
