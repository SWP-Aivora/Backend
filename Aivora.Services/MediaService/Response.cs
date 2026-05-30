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
}
