using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using ScoutsAttendance.Application.Interfaces;

namespace ScoutsAttendance.Infrastructure.Services;

/// <summary>
/// Photo storage backed by Cloudinary CDN.
/// Activated automatically when the CLOUDINARY_URL environment variable is set.
/// Format: cloudinary://api_key:api_secret@cloud_name
/// </summary>
public class CloudinaryPhotoService : IPhotoService
{
    private readonly Cloudinary _cloudinary;
    private readonly HttpClient _http;

    public CloudinaryPhotoService(HttpClient http)
    {
        // Cloudinary() without arguments reads CLOUDINARY_URL from the environment
        _cloudinary = new Cloudinary();
        _cloudinary.Api.Secure = true;
        _http = http;
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string memberId)
    {
        var upload = new ImageUploadParams
        {
            File        = new FileDescription(fileName, fileStream),
            PublicId    = $"scouts/members/{memberId}",
            Overwrite   = true,
            // Auto-crop to a 400×400 face-centred square
            Transformation = new Transformation()
                .Width(400).Height(400).Crop("fill").Gravity("face")
        };

        var result = await _cloudinary.UploadAsync(upload);

        if (result.Error is not null)
            throw new InvalidOperationException(
                $"Cloudinary upload failed: {result.Error.Message}");

        return result.SecureUrl.ToString();
    }

    public async Task<byte[]?> GetPhotoBytesAsync(string imageUrl)
    {
        try
        {
            return await _http.GetByteArrayAsync(imageUrl);
        }
        catch
        {
            return null;
        }
    }

    public async Task DeleteAsync(string imageUrl)
    {
        try
        {
            // Extract the Cloudinary public_id from the secure URL.
            // URL format: https://res.cloudinary.com/{cloud}/image/upload/[v{ver}/]{public_id}.{ext}
            var uri      = new Uri(imageUrl);
            var segments = uri.Segments; // each segment ends with '/'

            var uploadIdx = Array.FindIndex(segments, s => s.TrimEnd('/') == "upload");
            if (uploadIdx < 0) return;

            var afterUpload = segments.Skip(uploadIdx + 1).ToArray();

            // Skip optional version segment (v1234567890)
            if (afterUpload.Length > 0)
            {
                var first = afterUpload[0].TrimEnd('/');
                if (first.StartsWith('v') && first[1..].All(char.IsDigit))
                    afterUpload = afterUpload.Skip(1).ToArray();
            }

            var publicIdWithExt = string.Concat(afterUpload).TrimStart('/');
            var publicId        = Path.ChangeExtension(publicIdWithExt, null)?.TrimEnd('.') ?? publicIdWithExt;

            await _cloudinary.DestroyAsync(new DeletionParams(publicId));
        }
        catch
        {
            // Best-effort deletion — never throw.
        }
    }
}
