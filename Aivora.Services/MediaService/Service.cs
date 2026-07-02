using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Aivora.Services.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Aivora.Services.Exceptions;

namespace Aivora.Services.MediaService;

public class Service : IService
{
    private static readonly HashSet<string> AllowedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "general",
        "avatars",
        "profiles",
        "jobs",
        "deliverables",
        "disputes",
        "chat",
        "certificates"
    };

    private static readonly Dictionary<string, string[]> ImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = new[] { "image/jpeg" },
        [".jpeg"] = new[] { "image/jpeg" },
        [".png"] = new[] { "image/png" },
        [".webp"] = new[] { "image/webp" }
    };

    private static readonly Dictionary<string, string[]> FileContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = new[] { "application/pdf" },
        [".zip"] = new[] { "application/zip", "application/x-zip-compressed" },
        [".rar"] = new[] { "application/vnd.rar", "application/x-rar-compressed", "application/octet-stream" },
        [".docx"] = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        [".txt"] = new[] { "text/plain" }
    };

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
        await ValidateFileAsync(file, ImageContentTypes, 5 * 1024 * 1024);
        var cloudinaryFolder = NormalizeFolder(folder);

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(Path.GetFileName(file.FileName), file.OpenReadStream()),
            Folder = cloudinaryFolder,
            Transformation = new Transformation().Quality("auto").FetchFormat("auto")
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);

        if (uploadResult.Error != null)
            throw new InvalidOperationException("Cloudinary upload failed.");

        return new Response.UploadResponse
        {
            Url = uploadResult.SecureUrl.ToString(),
            PublicId = uploadResult.PublicId,
            Format = uploadResult.Format,
            Bytes = uploadResult.Bytes
        };
    }

    public async Task<Response.UploadResponse> UploadFileAsync(IFormFile file, string folder = "deliverables")
    {
        await ValidateFileAsync(file, FileContentTypes, 20 * 1024 * 1024);
        var cloudinaryFolder = NormalizeFolder(folder);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        string secureUrl;
        string publicId;
        string format;
        long bytes;

        if (extension == ".pdf")
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(Path.GetFileName(file.FileName), file.OpenReadStream()),
                Folder = cloudinaryFolder
            };
            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            if (uploadResult.Error != null)
                throw new InvalidOperationException("Cloudinary upload failed.");
            secureUrl = uploadResult.SecureUrl.ToString();
            publicId = uploadResult.PublicId;
            format = uploadResult.Format;
            bytes = uploadResult.Bytes;
        }
        else
        {
            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(Path.GetFileName(file.FileName), file.OpenReadStream()),
                Folder = cloudinaryFolder
            };
            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            if (uploadResult.Error != null)
                throw new InvalidOperationException("Cloudinary upload failed.");
            secureUrl = uploadResult.SecureUrl.ToString();
            publicId = uploadResult.PublicId;
            format = uploadResult.Format;
            bytes = uploadResult.Bytes;
        }

        return new Response.UploadResponse
        {
            Url = secureUrl,
            PublicId = publicId,
            Format = format,
            Bytes = bytes
        };
    }

    public async Task DeleteMediaAsync(string publicId)
    {
        var deletionParams = new DeletionParams(publicId);
        await _cloudinary.DestroyAsync(deletionParams);
    }

    private static async Task ValidateFileAsync(IFormFile file, IReadOnlyDictionary<string, string[]> allowedTypes, long maxSizeBytes)
    {
        if (file == null || file.Length == 0)
            throw new ValidationException("File is empty.");

        if (file.Length > maxSizeBytes)
            throw new ValidationException($"File size exceeds the limit of {maxSizeBytes / 1024 / 1024}MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !allowedTypes.TryGetValue(extension, out var allowedContentTypes))
            throw new ValidationException($"File extension {extension} is not allowed. Allowed: {string.Join(", ", allowedTypes.Keys)}");

        if (string.IsNullOrWhiteSpace(file.ContentType)
            || !allowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException("File content type is not allowed for the supplied extension.");
        }

        await ValidateSignatureAsync(file, extension);
    }

    private static string NormalizeFolder(string folder)
    {
        var normalized = string.IsNullOrWhiteSpace(folder) ? "general" : folder.Trim().ToLowerInvariant();
        if (!AllowedFolders.Contains(normalized))
        {
            throw new ValidationException("Upload folder is not allowed.");
        }

        return $"aivora/{normalized}";
    }

    private static async Task ValidateSignatureAsync(IFormFile file, string extension)
    {
        var buffer = new byte[(int)Math.Min(file.Length, 12)];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));

        var valid = extension switch
        {
            ".jpg" or ".jpeg" => StartsWith(buffer, bytesRead, new byte[] { 0xFF, 0xD8, 0xFF }),
            ".png" => StartsWith(buffer, bytesRead, new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
            ".webp" => StartsWith(buffer, bytesRead, new byte[] { 0x52, 0x49, 0x46, 0x46 }) && HasBytesAt(buffer, bytesRead, 8, new byte[] { 0x57, 0x45, 0x42, 0x50 }),
            ".pdf" => StartsWith(buffer, bytesRead, new byte[] { 0x25, 0x50, 0x44, 0x46 }),
            ".zip" or ".docx" => StartsWith(buffer, bytesRead, new byte[] { 0x50, 0x4B, 0x03, 0x04 })
                || StartsWith(buffer, bytesRead, new byte[] { 0x50, 0x4B, 0x05, 0x06 })
                || StartsWith(buffer, bytesRead, new byte[] { 0x50, 0x4B, 0x07, 0x08 }),
            ".rar" => StartsWith(buffer, bytesRead, new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07 }),
            ".txt" => bytesRead > 0 && buffer.Take(bytesRead).All(value => value != 0),
            _ => false
        };

        if (!valid)
        {
            throw new ValidationException("File signature does not match the supplied extension.");
        }
    }

    private static bool StartsWith(byte[] buffer, int bytesRead, byte[] signature)
    {
        if (bytesRead < signature.Length)
        {
            return false;
        }

        for (var i = 0; i < signature.Length; i++)
        {
            if (buffer[i] != signature[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasBytesAt(byte[] buffer, int bytesRead, int offset, byte[] signature)
    {
        if (bytesRead < offset + signature.Length)
        {
            return false;
        }

        for (var i = 0; i < signature.Length; i++)
        {
            if (buffer[offset + i] != signature[i])
            {
                return false;
            }
        }

        return true;
    }
}
