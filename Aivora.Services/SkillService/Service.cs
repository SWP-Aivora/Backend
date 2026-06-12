using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.SkillService;

public class SkillApplicationService : IService
{
    private readonly AivoraDbContext _dbContext;

    public SkillApplicationService(AivoraDbContext dbContext)
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

    public async Task<Response.ExpertSkillResponse> AddExpertSkillAsync(Guid userId, Request.AddExpertSkillRequest request)
    {
        var expert = await _dbContext.ExpertProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (expert == null) throw new NotFoundException("Expert profile not found.");

        var skill = await _dbContext.Skills.FindAsync(request.SkillId);
        if (skill == null) throw new NotFoundException("Skill not found.");

        var exists = await _dbContext.ExpertSkills.AnyAsync(es => es.ExpertId == expert.Id && es.SkillId == request.SkillId);
        if (exists) throw new ValidationException("Expert already has this skill.");

        var expertSkill = new ExpertSkill
        {
            ExpertId = expert.Id,
            SkillId = request.SkillId,
            Level = request.Level,
            YearsExperience = request.YearsExperience
        };

        _dbContext.ExpertSkills.Add(expertSkill);
        await _dbContext.SaveChangesAsync();

        return new Response.ExpertSkillResponse
        {
            Id = expertSkill.Id,
            ExpertId = expertSkill.ExpertId,
            SkillId = expertSkill.SkillId,
            SkillName = skill.Name,
            Level = expertSkill.Level.ToString(),
            YearsExperience = expertSkill.YearsExperience
        };
    }

    public async Task<bool> RemoveExpertSkillAsync(Guid userId, Guid skillId)
    {
        var expert = await _dbContext.ExpertProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (expert == null) throw new NotFoundException("Expert profile not found.");

        var expertSkill = await _dbContext.ExpertSkills.FirstOrDefaultAsync(es => es.ExpertId == expert.Id && es.SkillId == skillId);
        if (expertSkill == null) throw new NotFoundException("Expert does not have this skill.");

        _dbContext.ExpertSkills.Remove(expertSkill);
        await _dbContext.SaveChangesAsync();
        return true;
    }
}
