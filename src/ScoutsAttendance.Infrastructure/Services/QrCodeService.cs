using QRCoder;
using ScoutsAttendance.Application.Interfaces;

namespace ScoutsAttendance.Infrastructure.Services;

public class QrCodeService : IQrCodeService
{
    private const string Prefix = "SCOUT-";

    /// <summary>Generates a QR token from a 6-digit CustomId, e.g. "SCOUT-100001".</summary>
    public string GenerateQrCodeToken(int customId) => $"{Prefix}{customId}";

    public byte[] GenerateQrCodeImage(string data)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        return qrCode.GetGraphic(20);
    }

    /// <summary>Decodes a "SCOUT-XXXXXX" token back to the 6-digit CustomId integer.</summary>
    public int? DecodeQrToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        if (!token.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return null;

        var raw = token[Prefix.Length..];
        return int.TryParse(raw, out var id) ? id : null;
    }
}
