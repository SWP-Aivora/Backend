namespace Aivora.Services.MessageService;

public class Request
{
    public class SendMessageRequest
    {
        public Guid ConversationId { get; set; }
        public string? Content { get; set; }
        public string? AttachmentUrl { get; set; }
    }
}
