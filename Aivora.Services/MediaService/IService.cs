using Microsoft.AspNetCore.Http;

namespace Aivora.Services.MediaService;

public interface IService
{
    Task<Response.UploadResponse> UploadImageAsync(IFormFile file, Guid userId, string folder = "avatars");
    Task<Response.UploadResponse> UploadFileAsync(IFormFile file, Guid userId, string folder = "deliverables");
    Task DeleteMediaAsync(string publicId, Guid requesterId, bool isAdmin);
    Task<List<Response.MediaItemResponse>> ListMediaAsync(Guid userId);
}
