namespace Aivora.Services.NotificationService;

public class Response
{
    public class NotificationResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string? LinkUrl { get; set; }
        public bool IsRead { get; set; }
        public string? Type { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
