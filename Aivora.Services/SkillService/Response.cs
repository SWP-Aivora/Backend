namespace Aivora.Services.SkillService;

public class Response
{
    public class SkillResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }
    }

    public class ExpertSkillResponse
    {
        public Guid Id { get; set; }
        public Guid ExpertId { get; set; }
        public Guid SkillId { get; set; }
        public string SkillName { get; set; } = null!;
        public string Level { get; set; } = null!;
        public int YearsExperience { get; set; }
    }
}
