namespace Aivora.Services.SkillService;

public class Request
{
    public class CreateSkillRequest
    {
        public string Name { get; set; } = null!;
        public Guid? CategoryId { get; set; }
    }
}
