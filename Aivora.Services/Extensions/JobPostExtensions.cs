using Aivora.Repositories.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aivora.Services.Extensions;

public static class JobPostExtensions
{
    public static IQueryable<JobPost> IncludeSkills(this IQueryable<JobPost> query) =>
        query.Include(j => j.JobSkills).ThenInclude(js => js.Skill);
}
