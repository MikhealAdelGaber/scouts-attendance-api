using Microsoft.AspNetCore.Hosting;
using ScoutsAttendance.Application.Interfaces;

namespace ScoutsAttendance.Infrastructure.Services;

/// <summary>
/// Photo storage that saves files to wwwroot/uploads/members/.
/// Used when CLOUDINARY_URL is not set (local dev / non-Railway deployments).
/// Requires app.UseStaticFiles() in Program.cs.
/// </summary>
public class LocalPhotoService : IPhotoService
{
    private readonly IWebHostEnvironment _env;

    public LocalPhotoService(IWebHostEnvironment env)
    {
        _env = env;
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

        return $"/uploads/members/{memberId}{ext}";
    }

    public async Task<byte[]?> GetPhotoBytesAsync(string imageUrl)
    {
        var webRoot = _env.WebRootPath
            ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        var relative  = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
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

            var relative  = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var localPath = Path.Combine(webRoot, relative);

            if (File.Exists(localPath))
                File.Delete(localPath);
        }
        catch
        {
            // Best-effort — do not bubble storage errors up to the caller.
        }
        return Task.CompletedTask;
    }
}
