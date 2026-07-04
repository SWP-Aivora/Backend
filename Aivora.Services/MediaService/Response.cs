namespace Aivora.Services.MediaService;

public class Response
{
    public class UploadResponse
    {
        public string Url { get; set; } = null!;
        public string PublicId { get; set; } = null!;
        public string Format { get; set; } = null!;
        public long Bytes { get; set; }
    }

    public class MediaItemResponse
    {
        public string Url { get; set; } = null!;
        public string PublicId { get; set; } = null!;
        public string Format { get; set; } = null!;
        public long Bytes { get; set; }
        public string CreatedAt { get; set; } = null!;
    }
}
