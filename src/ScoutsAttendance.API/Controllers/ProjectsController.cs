using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Projects;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService     _service;
    private readonly IExcelExportService _excel;

    public ProjectsController(IProjectService service, IExcelExportService excel)
    {
        _service = service;
        _excel   = excel;
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProjectDto>>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<IEnumerable<ProjectDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null
            ? NotFound(ApiResponse.Fail("Project not found"))
            : Ok(ApiResponse<ProjectDto>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> Create([FromBody] CreateProjectDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id },
                ApiResponse<ProjectDto>.Ok(result, "Project created"));
        }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> Update(Guid id, [FromBody] UpdateProjectDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return result is null
            ? NotFound(ApiResponse.Fail("Project not found"))
            : Ok(ApiResponse<ProjectDto>.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        var ok = await _service.DeleteAsync(id);
        return ok ? Ok(ApiResponse.Ok("Project deleted")) : NotFound(ApiResponse.Fail("Project not found"));
    }

    // ── Grading ───────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ProjectMemberScoreDto>>>> GetMembers(Guid id)
    {
        var result = await _service.GetProjectMembersAsync(id);
        return Ok(ApiResponse<IEnumerable<ProjectMemberScoreDto>>.Ok(result));
    }

    [HttpPost("{id:guid}/members/{memberId:guid}/score")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<ProjectMemberScoreDto>>> SaveScore(
        Guid id, Guid memberId, [FromBody] SaveScoreDto dto)
    {
        try
        {
            var result = await _service.SaveScoreAsync(id, memberId, dto);
            return result is null
                ? NotFound(ApiResponse.Fail("Project not found"))
                : Ok(ApiResponse<ProjectMemberScoreDto>.Ok(result, "Score saved"));
        }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }

    // ── Export ────────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/export")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<IActionResult> ExportResults(Guid id)
    {
        var project = await _service.GetByIdAsync(id);
        if (project is null) return NotFound("Project not found");

        var members = await _service.GetProjectMembersAsync(id);
        var bytes   = await _excel.ExportProjectResultsAsync(project, members);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Project-{project.Name.Replace(' ', '-')}.xlsx");
    }
}

// ─── Member-scoped project endpoints ─────────────────────────────────────────

[ApiController]
[Route("api/members/{memberId:guid}/projects")]
[Authorize]
public class MemberProjectsController : ControllerBase
{
    private readonly IProjectService _service;

    public MemberProjectsController(IProjectService service) => _service = service;

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<MemberProjectSummaryDto>>> GetSummary(Guid memberId)
    {
        var result = await _service.GetMemberSummaryAsync(memberId);
        return Ok(ApiResponse<MemberProjectSummaryDto>.Ok(result));
    }
}
