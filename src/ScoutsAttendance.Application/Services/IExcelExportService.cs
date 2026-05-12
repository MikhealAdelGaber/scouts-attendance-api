namespace ScoutsAttendance.Application.Services;

/// <summary>Generates Excel workbooks for the major data exports.</summary>
public interface IExcelExportService
{
    Task<byte[]> ExportMembersAsync(Guid? troopId = null);
    Task<byte[]> ExportAttendanceAsync(Guid? eventId, Guid? troopId, DateTime? from, DateTime? to);
    Task<byte[]> ExportPointsAsync(Guid? troopId = null);
    Task<byte[]> ExportTroopPointsAsync(Guid? troopId = null);
    Task<byte[]> ExportExamScoresAsync(Guid? troopId = null, int? year = null);
}
