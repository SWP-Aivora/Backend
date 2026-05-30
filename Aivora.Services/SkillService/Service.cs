using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.SkillService;

public class Service : IService
{
    private readonly AivoraDbContext _dbContext;

    public Service(AivoraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Response.SkillResponse>> GetSkillsAsync(Guid? categoryId = null)
    {
        var query = _dbContext.Skills.AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(s => s.CategoryId == categoryId.Value);
        }

        return await query
            .Select(s => new Response.SkillResponse
            {
                Id = s.Id,
                Name = s.Name,
                CategoryId = s.CategoryId,
                CategoryName = s.Category != null ? s.Category.Name : null
            })
            .ToListAsync();
    }

    public async Task<Response.SkillResponse> GetSkillByIdAsync(Guid id)
    {
        var skill = await _dbContext.Skills
            .Include(s => s.Category)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (skill == null) throw new NotFoundException("Skill not found.");

        return new Response.SkillResponse
        {
            Id = skill.Id,
            Name = skill.Name,
            CategoryId = skill.CategoryId,
            CategoryName = skill.Category?.Name
        };
    }

    public async Task<Response.SkillResponse> CreateSkillAsync(Request.CreateSkillRequest request)
    {
        var exists = await _dbContext.Skills.AnyAsync(s => s.Name == request.Name);
        if (exists) throw new ValidationException("Skill name already exists.");

        if (request.CategoryId.HasValue)
        {
            var categoryExists = await _dbContext.Categories.AnyAsync(c => c.Id == request.CategoryId.Value);
            if (!categoryExists) throw new ValidationException("Category not found.");
        }

        var skill = new Skill
        {
            Name = request.Name,
            CategoryId = request.CategoryId
        };

        _dbContext.Skills.Add(skill);
        await _dbContext.SaveChangesAsync();

        return new Response.SkillResponse
        {
            Id = skill.Id,
            Name = skill.Name,
            CategoryId = skill.CategoryId
        };
    }
}
