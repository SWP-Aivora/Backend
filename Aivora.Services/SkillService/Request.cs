using Aivora.Repositories.Enums;

namespace Aivora.Services.SkillService;

public class Request
{
    public class CreateSkillRequest
    {
        public string Name { get; set; } = null!;
        public Guid? CategoryId { get; set; }
    }

    public class AddExpertSkillRequest
    {
        public Guid SkillId { get; set; }
        public SkillLevel Level { get; set; }
        public int YearsExperience { get; set; }
    }
}
