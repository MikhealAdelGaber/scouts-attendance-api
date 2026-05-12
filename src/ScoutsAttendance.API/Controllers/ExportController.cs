using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly IExportService      _csv;
    private readonly IExcelExportService _excel;

    public ExportController(IExportService csv, IExcelExportService excel)
    {
        _csv   = csv;
        _excel = excel;
    }

    private static string Stamp() => DateTime.UtcNow.ToString("yyyyMMdd_HHmm");

    // ─── CSV ───────────────────────────────────────────────────────────────────

    /// <summary>Export attendance as CSV.</summary>
    [HttpGet("attendance")]
    [HttpGet("attendance/csv")]
    public async Task<IActionResult> AttendanceCsv(
        [FromQuery] Guid?     eventId,
        [FromQuery] Guid?     troopId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var bytes = await _csv.ExportAttendanceCsvAsync(eventId, troopId, from, to);
        return File(bytes, "text/csv", $"attendance_{Stamp()}.csv");
    }

    // ─── Excel ─────────────────────────────────────────────────────────────────

    /// <summary>Export members list as Excel.</summary>
    [HttpGet("members/excel")]
    public async Task<IActionResult> MembersExcel([FromQuery] Guid? troopId)
    {
        var bytes = await _excel.ExportMembersAsync(troopId);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"members_{Stamp()}.xlsx");
    }

    /// <summary>Export attendance as Excel.</summary>
    [HttpGet("attendance/excel")]
    public async Task<IActionResult> AttendanceExcel(
        [FromQuery] Guid?     eventId,
        [FromQuery] Guid?     troopId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var bytes = await _excel.ExportAttendanceAsync(eventId, troopId, from, to);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"attendance_{Stamp()}.xlsx");
    }

    /// <summary>Export member points as Excel.</summary>
    [HttpGet("points/excel")]
    public async Task<IActionResult> PointsExcel([FromQuery] Guid? troopId)
    {
        var bytes = await _excel.ExportPointsAsync(troopId);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"points_{Stamp()}.xlsx");
    }

    /// <summary>Export troop points as Excel.</summary>
    [HttpGet("troop-points/excel")]
    public async Task<IActionResult> TroopPointsExcel([FromQuery] Guid? troopId)
    {
        var bytes = await _excel.ExportTroopPointsAsync(troopId);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"troop_points_{Stamp()}.xlsx");
    }

    /// <summary>Export exam scores as Excel.</summary>
    [HttpGet("exam-scores/excel")]
    public async Task<IActionResult> ExamScoresExcel(
        [FromQuery] Guid? troopId,
        [FromQuery] int?  year)
    {
        var bytes = await _excel.ExportExamScoresAsync(troopId, year);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"exam_scores_{Stamp()}.xlsx");
    }
}
