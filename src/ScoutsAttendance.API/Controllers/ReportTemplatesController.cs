using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Reports;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/report-templates")]
[Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
public class ReportTemplatesController : ControllerBase
{
    private readonly IReportService      _report;
    private readonly IExcelExportService _excel;

    public ReportTemplatesController(IReportService report, IExcelExportService excel)
    {
        _report = report;
        _excel  = excel;
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<ReportTemplateDto>>>> GetAll()
    {
        var result = await _report.GetAllAsync();
        return Ok(ApiResponse<IEnumerable<ReportTemplateDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ReportTemplateDto>>> GetById(Guid id)
    {
        var result = await _report.GetByIdAsync(id);
        return result is null
            ? NotFound(ApiResponse.Fail("Template not found"))
            : Ok(ApiResponse<ReportTemplateDto>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ReportTemplateDto>>> Create(
        [FromBody] CreateReportTemplateDto dto)
    {
        try
        {
            var result = await _report.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                ApiResponse<ReportTemplateDto>.Ok(result, "Report template created"));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ReportTemplateDto>>> Update(
        Guid id, [FromBody] UpdateReportTemplateDto dto)
    {
        try
        {
            var result = await _report.UpdateAsync(id, dto);
            return result is null
                ? NotFound(ApiResponse.Fail("Template not found"))
                : Ok(ApiResponse<ReportTemplateDto>.Ok(result));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        var ok = await _report.DeleteAsync(id);
        return ok ? Ok(ApiResponse.Ok("Template deleted")) : NotFound(ApiResponse.Fail("Template not found"));
    }

    // ── Results ───────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/results")]
    public async Task<ActionResult<ApiResponse<ReportResultsDto>>> GetResults(
        Guid id, [FromQuery] decimal passThreshold = 50m)
    {
        var result = await _report.GetResultsAsync(id, passThreshold);
        return result is null
            ? NotFound(ApiResponse.Fail("Template not found"))
            : Ok(ApiResponse<ReportResultsDto>.Ok(result));
    }

    // ── Custom scores ─────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/custom-scores/{categoryId:guid}")]
    public async Task<ActionResult<ApiResponse<CategoryCustomScoresDto>>> GetCustomScores(
        Guid id, Guid categoryId)
    {
        var result = await _report.GetCustomScoresAsync(id, categoryId);
        return result is null
            ? NotFound(ApiResponse.Fail("Template or category not found"))
            : Ok(ApiResponse<CategoryCustomScoresDto>.Ok(result));
    }

    [HttpPost("{id:guid}/custom-scores")]
    public async Task<ActionResult<ApiResponse>> SaveCustomScores(
        Guid id, [FromBody] SaveCustomScoresDto dto)
    {
        try
        {
            var count = await _report.SaveCustomScoresAsync(id, dto);
            return Ok(ApiResponse.Ok($"{count} scores saved"));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
        catch (ArgumentException ex)         { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }

    // ── Excel export ──────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> ExportExcel(Guid id, [FromQuery] decimal passThreshold = 50m)
    {
        var results = await _report.GetResultsAsync(id, passThreshold);
        if (results is null) return NotFound("Template not found");

        var bytes    = await _excel.ExportFinalReportAsync(results);
        var filename = $"Final-Report-{results.Template.Name.Replace(' ', '-')}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            filename);
    }
}

// ─── Member-scoped endpoint ───────────────────────────────────────────────────

[ApiController]
[Route("api/members/{memberId:guid}/report-results")]
[Authorize]
public class MemberReportResultsController : ControllerBase
{
    private readonly IReportService _report;
    public MemberReportResultsController(IReportService report) => _report = report;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<MemberReportSummaryDto>>>> GetMemberResults(
        Guid memberId)
    {
        var results = await _report.GetMemberResultsAsync(memberId);
        return Ok(ApiResponse<List<MemberReportSummaryDto>>.Ok(results));
    }
}
