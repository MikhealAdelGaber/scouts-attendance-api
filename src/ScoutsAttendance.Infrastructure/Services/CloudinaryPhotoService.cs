using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using ScoutsAttendance.Application.Interfaces;

namespace ScoutsAttendance.Infrastructure.Services;

/// <summary>
/// Photo storage backed by Cloudinary CDN.
///
/// Supports two configuration styles (checked in order):
///   1. CLOUDINARY_URL  = cloudinary://api_key:api_secret@cloud_name
///   2. CLOUDINARY_CLOUD_NAME + CLOUDINARY_API_KEY + CLOUDINARY_API_SECRET  (individual vars)
/// </summary>
public class CloudinaryPhotoService : IPhotoService
{
    private readonly Cloudinary _cloudinary;
    private readonly HttpClient _http;

    public CloudinaryPhotoService(HttpClient http)
    {
        _http = http;

        // ── Option 1: single CLOUDINARY_URL env var ───────────────────────────
        var url = Environment.GetEnvironmentVariable("CLOUDINARY_URL");
        if (!string.IsNullOrWhiteSpace(url))
        {
            _cloudinary = new Cloudinary(url);
        }
        else
        {
            // ── Option 2: three separate env vars ────────────────────────────
            var cloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME") ?? "";
            var apiKey    = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY")    ?? "";
            var apiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET") ?? "";

            var account   = new Account(cloudName, apiKey, apiSecret);
            _cloudinary   = new Cloudinary(account);
        }

        _cloudinary.Api.Secure = true;
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
        try { return await _http.GetByteArrayAsync(imageUrl); }
        catch { return null; }
    }

    public async Task DeleteAsync(string imageUrl)
    {
        try
        {
            var uri      = new Uri(imageUrl);
            var segments = uri.Segments;

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
        catch { /* best-effort */ }
    }
}
