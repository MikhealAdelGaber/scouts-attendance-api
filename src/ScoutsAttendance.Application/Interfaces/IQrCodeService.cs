namespace ScoutsAttendance.Application.Interfaces;

public interface IQrCodeService
{
    /// <summary>Generates a QR token from a 6-digit CustomId (e.g. "SCOUT-100001").</summary>
    string GenerateQrCodeToken(int customId);

    byte[] GenerateQrCodeImage(string data);

    /// <summary>Decodes a QR token back to the 6-digit CustomId, or null if invalid.</summary>
    int? DecodeQrToken(string token);
}
