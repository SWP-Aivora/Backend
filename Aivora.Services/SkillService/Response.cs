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
}
