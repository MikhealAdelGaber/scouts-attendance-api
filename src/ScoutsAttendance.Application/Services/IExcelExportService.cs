using ScoutsAttendance.Application.DTOs.Reports;

namespace ScoutsAttendance.Application.Services;

/// <summary>Generates Excel workbooks for the major data exports.</summary>
public interface IExcelExportService
{
    Task<byte[]> ExportMembersAsync(Guid? troopId = null);

    /// <summary>Full member export — includes CustomId, Gender, Notes, TotalPoints.</summary>
    Task<byte[]> ExportMembersFullAsync(Guid? troopId = null);

    Task<byte[]> ExportAttendanceAsync(Guid? eventId, Guid? troopId, DateTime? from, DateTime? to);

    /// <summary>Per-member attendance rate report for a given date range.</summary>
    Task<byte[]> ExportAttendanceRateAsync(Guid? troopId, DateTime? from, DateTime? to);

    /// <summary>Returns the rate data as a list for the frontend preview table.</summary>
    Task<IEnumerable<AttendanceRateDto>> GetAttendanceRateAsync(Guid? troopId, DateTime? from, DateTime? to);

    Task<byte[]> ExportPointsAsync(Guid? troopId = null);
    Task<byte[]> ExportTroopPointsAsync(Guid? troopId = null);
    Task<byte[]> ExportExamScoresAsync(Guid? troopId = null, int? year = null);
}
