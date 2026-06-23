using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Aivora.Services.MediaService;

namespace Aivora.Tests.ApiContract;

public class FakeMediaService : IService
{
    public Task<Response.UploadResponse> UploadImageAsync(IFormFile file, string folder = "avatars")
    {
        return Task.FromResult(new Response.UploadResponse
        {
            Url = $"https://res.cloudinary.com/fake/image/upload/{folder}/{file.FileName}",
            PublicId = file.FileName,
            Format = "png",
            Bytes = file.Length
        });
    }

    public Task<Response.UploadResponse> UploadFileAsync(IFormFile file, string folder = "deliverables")
    {
        return Task.FromResult(new Response.UploadResponse
        {
            Url = $"https://res.cloudinary.com/fake/raw/upload/{folder}/{file.FileName}",
            PublicId = file.FileName,
            Format = "pdf",
            Bytes = file.Length
        });
    }

    public Task DeleteMediaAsync(string publicId)
    {
        return Task.CompletedTask;
    }
}
