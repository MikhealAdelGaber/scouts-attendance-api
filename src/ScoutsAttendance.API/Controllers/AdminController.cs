using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Admin;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

/// <summary>
/// SystemAdmin-only endpoints for high-privilege operations:
/// new-year reset, year archive management.
/// ALL endpoints require Role = SystemAdmin.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "SystemAdmin")]
public class AdminController : ControllerBase
{
    private readonly INewYearService      _newYear;
    private readonly IExcelExportService  _excel;

    public AdminController(INewYearService newYear, IExcelExportService excel)
    {
        _newYear = newYear;
        _excel   = excel;
    }

    // ── Password verification (step 2 of the dialog) ─────────────────────────

    /// <summary>
    /// POST /api/admin/verify-password
    /// Verifies the admin's password without changing any data.
    /// Used by the "Start New Year" dialog step 2 before showing step 3.
    /// </summary>
    [HttpPost("verify-password")]
    public async Task<ActionResult<ApiResponse>> VerifyPassword([FromBody] VerifyPasswordDto dto)
    {
        var (ok, error) = await _newYear.VerifyPasswordAsync(dto.Password);
        return ok
            ? Ok(ApiResponse.Ok("Password verified"))
            : BadRequest(ApiResponse.Fail(error));
    }

    // ── New-year reset ────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/admin/start-new-year
    /// Verifies password + "CONFIRM" text, archives all member stats, then
    /// deletes all MemberPoints, MemberExcuses, and PendingExcuses.
    /// Entire operation runs in a single DB transaction.
    /// </summary>
    [HttpPost("start-new-year")]
    public async Task<ActionResult<ApiResponse<NewYearResultDto>>> StartNewYear(
        [FromBody] StartNewYearDto dto)
    {
        var (ok, error, result) = await _newYear.StartAsync(dto);
        if (!ok) return BadRequest(ApiResponse.Fail(error));
        return Ok(ApiResponse<NewYearResultDto>.Ok(result!, "New year started successfully"));
    }

    // ── Archive list / detail / export ────────────────────────────────────────

    /// <summary>GET /api/admin/year-archives — list all yearly archive snapshots.</summary>
    [HttpGet("year-archives")]
    public async Task<ActionResult<ApiResponse<IEnumerable<YearlyArchiveSummaryDto>>>> GetArchives()
    {
        var list = await _newYear.GetArchivesAsync();
        return Ok(ApiResponse<IEnumerable<YearlyArchiveSummaryDto>>.Ok(list));
    }

    /// <summary>GET /api/admin/year-archives/{id} — get archive header + all member rows.</summary>
    [HttpGet("year-archives/{id:guid}")]
    public async Task<ActionResult<ApiResponse<YearlyArchiveDetailDto>>> GetArchive(Guid id)
    {
        var detail = await _newYear.GetArchiveByIdAsync(id);
        return detail is null
            ? NotFound(ApiResponse.Fail("Archive not found"))
            : Ok(ApiResponse<YearlyArchiveDetailDto>.Ok(detail));
    }

    /// <summary>GET /api/admin/year-archives/{id}/export — download archive as Excel.</summary>
    [HttpGet("year-archives/{id:guid}/export")]
    public async Task<IActionResult> ExportArchive(Guid id)
    {
        var detail = await _newYear.GetArchiveByIdAsync(id);
        if (detail is null) return NotFound("Archive not found");

        var bytes    = await _excel.ExportYearArchiveAsync(detail);
        var filename = $"Year-Archive-{detail.ArchiveYear}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            filename);
    }
}
