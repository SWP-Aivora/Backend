using Aivora.Repositories.Enums;

namespace Aivora.Services.JobInviteService;

public class Response
{
    public class JobInviteResponse
    {
        public Guid Id { get; set; }
        public Guid JobId { get; set; }
        public string JobTitle { get; set; } = null!;
        public Guid ExpertId { get; set; }
        public string ExpertName { get; set; } = null!;
        public Guid ClientId { get; set; }
        public JobInviteStatus Status { get; set; }
        public DateTimeOffset? RespondedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public Guid? ConversationId { get; set; }
    }
}
