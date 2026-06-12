using Aivora.Repositories.Data;
using Aivora.Repositories.Entities;
using Aivora.Repositories.Enums;
using Aivora.Services.SkillService;
using Aivora.Services.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aivora.Tests.Services;

public class SkillServiceTests
{
    private AivoraDbContext GetDbContext()
    {
        var options = new DbContextOptionsBuilder<AivoraDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AivoraDbContext(options);
    }

    [Fact]
    public async Task RemoveExpertSkillAsync_Succeeds_WhenSkillExists()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var expert = new ExpertProfile { Id = Guid.NewGuid(), UserId = userId, Title = "Dev" };
        var skill = new Skill { Id = Guid.NewGuid(), Name = "Dotnet" };
        var expertSkill = new ExpertSkill { ExpertId = expert.Id, SkillId = skill.Id };

        dbContext.ExpertProfiles.Add(expert);
        dbContext.Skills.Add(skill);
        dbContext.ExpertSkills.Add(expertSkill);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.SkillService.SkillApplicationService(dbContext);

        // Act
        var result = await service.RemoveExpertSkillAsync(userId, skill.Id);

        // Assert
        result.Should().BeTrue();
        (await dbContext.ExpertSkills.AnyAsync(es => es.ExpertId == expert.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task AddExpertSkillAsync_ThrowsValidation_WhenExpertAlreadyHasSkill()
    {
        // Arrange
        var dbContext = GetDbContext();
        var userId = Guid.NewGuid();
        var expert = new ExpertProfile { Id = Guid.NewGuid(), UserId = userId, Title = "Dev" };
        var skill = new Skill { Id = Guid.NewGuid(), Name = "Dotnet" };
        var expertSkill = new ExpertSkill { ExpertId = expert.Id, SkillId = skill.Id };

        dbContext.ExpertProfiles.Add(expert);
        dbContext.Skills.Add(skill);
        dbContext.ExpertSkills.Add(expertSkill);
        await dbContext.SaveChangesAsync();

        var service = new Aivora.Services.SkillService.SkillApplicationService(dbContext);
        var request = new Request.AddExpertSkillRequest { SkillId = skill.Id };

        // Act
        Func<Task> act = async () => await service.AddExpertSkillAsync(userId, request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateSkillAsync_ThrowsValidation_WhenCategoryNotFound()
    {
        // Arrange
        var dbContext = GetDbContext();
        var service = new Aivora.Services.SkillService.SkillApplicationService(dbContext);
        var request = new Request.CreateSkillRequest { Name = "S", CategoryId = Guid.NewGuid() };

        // Act
        Func<Task> act = async () => await service.CreateSkillAsync(request);

        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("Category not found.");
    }
}
