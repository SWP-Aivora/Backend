using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Aivora.Services.Exceptions;
using Aivora.Services.MediaService;

namespace Aivora.Tests.ApiContract;

public class FakeMediaService : IService
{
    private readonly ConcurrentDictionary<string, Guid> _owners = new();

    public Task<Response.UploadResponse> UploadImageAsync(IFormFile file, Guid userId, string folder = "avatars")
    {
        var publicId = $"{folder}/{Guid.NewGuid():N}_{file.FileName}";
        _owners[publicId] = userId;
        return Task.FromResult(new Response.UploadResponse
        {
            Url = $"https://res.cloudinary.com/fake/image/upload/{publicId}",
            PublicId = publicId,
            Format = "png",
            Bytes = file.Length
        });
    }

    public Task<Response.UploadResponse> UploadFileAsync(IFormFile file, Guid userId, string folder = "deliverables")
    {
        var publicId = $"{folder}/{Guid.NewGuid():N}_{file.FileName}";
        _owners[publicId] = userId;
        return Task.FromResult(new Response.UploadResponse
        {
            Url = $"https://res.cloudinary.com/fake/raw/upload/{publicId}",
            PublicId = publicId,
            Format = "pdf",
            Bytes = file.Length
        });
    }

    public Task DeleteMediaAsync(string publicId, Guid requesterId, bool isAdmin)
    {
        if (!isAdmin && _owners.TryGetValue(publicId, out var ownerId) && ownerId != requesterId)
            throw new UnauthorizedException("You do not have permission to delete this media.");

        return Task.CompletedTask;
    }

    public Task<List<Response.MediaItemResponse>> ListMediaAsync(Guid userId)
    {
        return Task.FromResult(new List<Response.MediaItemResponse>());
    }
}
