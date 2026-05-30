using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Aivora.Services.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Aivora.Services.Exceptions;

namespace Aivora.Services.MediaService;

public class Service : IService
{
    private readonly Cloudinary _cloudinary;

    public Service(IOptions<CloudinaryOptions> options)
    {
        var acc = new Account(
            options.Value.CloudName,
            options.Value.ApiKey,
            options.Value.ApiSecret
        );
        _cloudinary = new Cloudinary(acc);
    }

    public async Task<Response.UploadResponse> UploadImageAsync(IFormFile file, string folder = "avatars")
    {
        ValidateFile(file, new[] { ".jpg", ".jpeg", ".png", ".webp" }, 5 * 1024 * 1024);

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, file.OpenReadStream()),
            Folder = $"aivora/{folder}",
            Transformation = new Transformation().Quality("auto").FetchFormat("auto")
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);

        if (uploadResult.Error != null)
            throw new Exception($"Cloudinary upload failed: {uploadResult.Error.Message}");

        return new Response.UploadResponse
        {
            Url = uploadResult.SecureUrl.ToString(),
            PublicId = uploadResult.PublicId,
            Format = uploadResult.Format,
            Bytes = uploadResult.Length
        };
    }

    public async Task<Response.UploadResponse> UploadFileAsync(IFormFile file, string folder = "deliverables")
    {
        ValidateFile(file, new[] { ".pdf", ".zip", ".rar", ".docx", ".txt" }, 20 * 1024 * 1024);

        var uploadParams = new RawUploadParams
        {
            File = new FileDescription(file.FileName, file.OpenReadStream()),
            Folder = $"aivora/{folder}"
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);

        if (uploadResult.Error != null)
            throw new Exception($"Cloudinary upload failed: {uploadResult.Error.Message}");

        return new Response.UploadResponse
        {
            Url = uploadResult.SecureUrl.ToString(),
            PublicId = uploadResult.PublicId,
            Format = uploadResult.Format,
            Bytes = uploadResult.Length
        };
    }

    public async Task DeleteMediaAsync(string publicId)
    {
        var deletionParams = new DeletionParams(publicId);
        await _cloudinary.DestroyAsync(deletionParams);
    }

    private void ValidateFile(IFormFile file, string[] allowedExtensions, long maxSizeBytes)
    {
        if (file == null || file.Length == 0)
            throw new ValidationException("File is empty.");

        if (file.Length > maxSizeBytes)
            throw new ValidationException($"File size exceeds the limit of {maxSizeBytes / 1024 / 1024}MB.");

        var extension = Path.GetExtension(file.FileName).ToLower();
        if (!allowedExtensions.Contains(extension))
            throw new ValidationException($"File extension {extension} is not allowed. Allowed: {string.Join(", ", allowedExtensions)}");
    }
}
