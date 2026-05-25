namespace ScoutsAttendance.Application.Services;

/// <summary>Generates Excel and PDF exports for a single trip.</summary>
public interface ITripExportService
{
    /// <summary>Four-sheet workbook: Trip Info, Confirmed Bookings, Waiting List, Attendance.</summary>
    Task<(byte[] Bytes, string Filename)> ExportExcelAsync(Guid tripId);

    /// <summary>A4 PDF with header, summary, bookings tables, attendance, and footer.</summary>
    Task<(byte[] Bytes, string Filename)> ExportPdfAsync(Guid tripId, string exportedBy);
}
