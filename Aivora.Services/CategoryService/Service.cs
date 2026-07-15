using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Aivora.Services.CategoryService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;
    private readonly IMemoryCache _cache;

    public Service(AivoraDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<List<Response.CategoryResponse>> GetCategoriesAsync()
    {
        return await _dbContext.Categories
            .Select(c => new Response.CategoryResponse
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ParentId = c.ParentId
            })
            .ToListAsync();
    }

    public async Task<Response.CategoryResponse> GetCategoryByIdAsync(Guid id)
    {
        var category = await _dbContext.Categories.FindAsync(id);
        if (category == null) throw new NotFoundException("Category not found.");

        return new Response.CategoryResponse
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            ParentId = category.ParentId
        };
    }

    public async Task<Response.CategoryResponse> CreateCategoryAsync(Request.CreateCategoryRequest request)
    {
        var exists = await _dbContext.Categories.AnyAsync(c => c.Name == request.Name);
        if (exists) throw new ValidationException("Category name already exists.");

        var category = new Category
        {
            Name = request.Name,
            Description = request.Description,
            ParentId = request.ParentId
        };

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();
        
        _cache.Remove("AICategoriesData");

        return new Response.CategoryResponse
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            ParentId = category.ParentId
        };
    }

    public async Task<Dictionary<Guid, string>> GetCachedCategoryDictionaryAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync("AICategoriesData", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2); // Short cache to avoid stale data across scale-out instances
            return await _dbContext.Categories
                .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        }) ?? new Dictionary<Guid, string>();
    }
}
