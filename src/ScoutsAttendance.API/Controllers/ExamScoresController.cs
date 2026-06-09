using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.ExamScores;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/exam-scores")]
[Authorize]
public class ExamScoresController : ControllerBase
{
    private readonly IExamScoreService   _service;
    private readonly IExcelExportService _excel;
    private readonly ICurrentUserService _currentUser;

    public ExamScoresController(
        IExamScoreService   service,
        IExcelExportService excel,
        ICurrentUserService currentUser)
    {
        _service     = service;
        _excel       = excel;
        _currentUser = currentUser;
    }

    // ── Score reads ───────────────────────────────────────────────────────────

    [HttpGet("member/{memberId:guid}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ExamScoreDto>>>> GetByMember(Guid memberId)
    {
        var result = await _service.GetByMemberAsync(memberId);
        return Ok(ApiResponse<IEnumerable<ExamScoreDto>>.Ok(result));
    }

    [HttpGet("troop/{troopId:guid}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ExamScoreDto>>>> GetByTroop(
        Guid troopId, [FromQuery] int? year)
    {
        var result = await _service.GetByTroopAsync(troopId, year);
        return Ok(ApiResponse<IEnumerable<ExamScoreDto>>.Ok(result));
    }

    // ── Score writes ──────────────────────────────────────────────────────────

    [HttpPost]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin,AttendanceOnly")]
    public async Task<ActionResult<ApiResponse<ExamScoreDto>>> Create([FromBody] CreateExamScoreDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return Ok(ApiResponse<ExamScoreDto>.Ok(result, "Exam score saved"));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin,AttendanceOnly")]
    public async Task<ActionResult<ApiResponse<ExamScoreDto>>> Update(Guid id, [FromBody] UpdateExamScoreDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return result is null
            ? NotFound(ApiResponse.Fail("Exam score not found"))
            : Ok(ApiResponse<ExamScoreDto>.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin,AttendanceOnly")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        var ok = await _service.DeleteAsync(id);
        return ok ? Ok(ApiResponse.Ok("Deleted")) : NotFound(ApiResponse.Fail("Not found"));
    }

    // ── Config ────────────────────────────────────────────────────────────────

    /// <summary>Get exam score config (max scores) for the current user's group and given year.</summary>
    [HttpGet("config/{year:int}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin,AttendanceOnly")]
    public async Task<ActionResult<ApiResponse<ExamScoreConfigDto?>>> GetConfig(int year)
    {
        var groupId = _currentUser.GroupId
            ?? throw new InvalidOperationException("No group context.");
        var result = await _service.GetConfigAsync(groupId, year);
        return Ok(ApiResponse<ExamScoreConfigDto?>.Ok(result));
    }

    /// <summary>Save (create or update) exam score config for the current user's group.</summary>
    [HttpPut("config")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse<ExamScoreConfigDto>>> SaveConfig([FromBody] SaveExamScoreConfigDto dto)
    {
        var groupId = _currentUser.GroupId
            ?? throw new InvalidOperationException("No group context.");
        var result = await _service.SaveConfigAsync(groupId, dto);
        return Ok(ApiResponse<ExamScoreConfigDto>.Ok(result, "Config saved"));
    }

    // ── Export Template ───────────────────────────────────────────────────────

    /// <summary>Download a pre-filled Excel template for bulk score entry.</summary>
    [HttpGet("export-template")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin,AttendanceOnly")]
    public async Task<IActionResult> ExportTemplate([FromQuery] int year, [FromQuery] Guid? troopId)
    {
        var groupId = _currentUser.GroupId
            ?? throw new InvalidOperationException("No group context.");
        var bytes = await _excel.ExportExamScoreTemplateAsync(groupId, year, troopId);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"ExamScores_Template_{year}.xlsx");
    }

    // ── Import ────────────────────────────────────────────────────────────────

    /// <summary>Import exam scores from a filled Excel file (max 5 MB).</summary>
    [HttpPost("import")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin,AttendanceOnly")]
    public async Task<ActionResult<ApiResponse<ImportExamScoreResultDto>>> Import(
        IFormFile file, [FromQuery] int year)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("No file uploaded."));

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(ApiResponse.Fail("File exceeds 5 MB limit."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx")
            return BadRequest(ApiResponse.Fail("Only .xlsx files are accepted."));

        var groupId = _currentUser.GroupId
            ?? throw new InvalidOperationException("No group context.");

        using var stream = file.OpenReadStream();
        var result = await _excel.ImportExamScoresAsync(stream, groupId, year);
        return Ok(ApiResponse<ImportExamScoreResultDto>.Ok(result,
            $"Import complete: {result.ImportedCount} saved, {result.SkippedCount} skipped."));
    }
}
