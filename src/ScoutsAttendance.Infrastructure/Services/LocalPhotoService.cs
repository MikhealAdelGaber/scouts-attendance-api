using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using ScoutsAttendance.Application.Interfaces;

namespace ScoutsAttendance.Infrastructure.Services;

/// <summary>
/// Photo storage that saves files to wwwroot/uploads/members/.
/// Used only when CLOUDINARY_URL is not set (local dev).
/// NOTE: Railway's filesystem is ephemeral — files are lost on redeploy.
///       Always set CLOUDINARY_URL in Railway for production use.
/// </summary>
public class LocalPhotoService : IPhotoService
{
    private readonly IWebHostEnvironment  _env;
    private readonly IHttpContextAccessor _httpCtx;

    public LocalPhotoService(IWebHostEnvironment env, IHttpContextAccessor httpCtx)
    {
        _env     = env;
        _httpCtx = httpCtx;
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string memberId)
    {
        var webRoot = _env.WebRootPath
            ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        var dir = Path.Combine(webRoot, "uploads", "members");
        Directory.CreateDirectory(dir);

        var ext  = Path.GetExtension(fileName).ToLowerInvariant();
        var file = Path.Combine(dir, $"{memberId}{ext}");

        await using var fs = File.Create(file);
        await fileStream.CopyToAsync(fs);

        // Return an absolute URL so the Angular frontend (on a different domain) can load the image.
        var relativePath = $"/uploads/members/{memberId}{ext}";
        var req = _httpCtx.HttpContext?.Request;
        if (req is not null)
        {
            var baseUrl = $"{req.Scheme}://{req.Host}";
            return $"{baseUrl}{relativePath}";
        }
        return relativePath;
    }

    public async Task<byte[]?> GetPhotoBytesAsync(string imageUrl)
    {
        var webRoot = _env.WebRootPath
            ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        // Strip scheme+host if it's an absolute URL so we can find the local file
        var path = imageUrl;
        try { path = new Uri(imageUrl).AbsolutePath; } catch { /* already relative */ }

        var relative  = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var localPath = Path.Combine(webRoot, relative);

        if (!File.Exists(localPath)) return null;
        return await File.ReadAllBytesAsync(localPath);
    }

    public Task DeleteAsync(string imageUrl)
    {
        try
        {
            var webRoot = _env.WebRootPath
                ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

            var path = imageUrl;
            try { path = new Uri(imageUrl).AbsolutePath; } catch { /* already relative */ }

            var relative  = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var localPath = Path.Combine(webRoot, relative);

            if (File.Exists(localPath))
                File.Delete(localPath);
        }
        catch { /* best-effort */ }
        return Task.CompletedTask;
    }
}
