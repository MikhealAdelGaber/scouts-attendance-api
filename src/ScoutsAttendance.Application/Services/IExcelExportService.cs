using ScoutsAttendance.Application.DTOs.Admin;
using ScoutsAttendance.Application.DTOs.Projects;
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

    /// <summary>Exports a yearly archive as a two-sheet workbook: summary + per-member stats.</summary>
    Task<byte[]> ExportYearArchiveAsync(YearlyArchiveDetailDto archive);

    /// <summary>Exports project results as Excel: all members with score, max, %, grade.</summary>
    Task<byte[]> ExportProjectResultsAsync(ProjectDto project, IEnumerable<ProjectMemberScoreDto> members);

    /// <summary>Exports badge awards as Excel with optional filters: troopId, category, from, to.</summary>
    Task<byte[]> ExportBadgesAsync(Guid? troopId, string? category, DateTime? from, DateTime? to);

    /// <summary>Exports a final report template result as Excel (stub — reserved for future use).</summary>
    Task<byte[]> ExportFinalReportAsync(object results);
}
