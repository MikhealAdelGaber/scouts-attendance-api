namespace ScoutsAttendance.Application.Services;

/// <summary>
/// Generates a print-ready A4 PDF containing one QR-code card per member,
/// grouped by Troop.  Scoping is applied automatically from the calling user's
/// JWT claims (SystemAdmin → all troops; GroupLeader → own group).
/// </summary>
public interface IQrPdfExportService
{
    /// <summary>
    /// Returns the PDF as a raw byte array and a suggested filename.
    /// </summary>
    Task<(byte[] Bytes, string Filename)> ExportAsync();
}
