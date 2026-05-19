namespace ScoutsAttendance.Application.Interfaces;

/// <summary>
/// Abstraction over photo storage.
/// LocalPhotoService saves to wwwroot/uploads/members/ (dev / no-Cloudinary).
/// CloudinaryPhotoService uploads to Cloudinary CDN (production via CLOUDINARY_URL env var).
/// </summary>
public interface IPhotoService
{
    /// <summary>
    /// Uploads an image and returns the public URL (Cloudinary) or relative path (local).
    /// </summary>
    Task<string> UploadAsync(Stream fileStream, string fileName, string memberId);

    /// <summary>
    /// Downloads the raw bytes for an image (for embedding in PDFs etc.).
    /// Returns null if the image does not exist or cannot be fetched.
    /// </summary>
    Task<byte[]?> GetPhotoBytesAsync(string imageUrl);

    /// <summary>
    /// Deletes the image from storage (best-effort; does not throw on failure).
    /// </summary>
    Task DeleteAsync(string imageUrl);
}
