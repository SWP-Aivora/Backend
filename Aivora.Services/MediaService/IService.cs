using Microsoft.AspNetCore.Http;

namespace Aivora.Services.MediaService;

public interface IService
{
    Task<Response.UploadResponse> UploadImageAsync(IFormFile file, string folder = "avatars");
    Task<Response.UploadResponse> UploadFileAsync(IFormFile file, string folder = "deliverables");
    Task DeleteMediaAsync(string publicId);
}
