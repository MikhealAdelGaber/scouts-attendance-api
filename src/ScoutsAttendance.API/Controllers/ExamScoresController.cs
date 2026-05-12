using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.ExamScores;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/exam-scores")]
[Authorize]
public class ExamScoresController : ControllerBase
{
    private readonly IExamScoreService _service;
    public ExamScoresController(IExamScoreService service) => _service = service;

    /// <summary>Get all exam scores for a member.</summary>
    [HttpGet("member/{memberId:guid}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ExamScoreDto>>>> GetByMember(Guid memberId)
    {
        var result = await _service.GetByMemberAsync(memberId);
        return Ok(ApiResponse<IEnumerable<ExamScoreDto>>.Ok(result));
    }

    /// <summary>Get all exam scores for a troop, optionally filtered by year.</summary>
    [HttpGet("troop/{troopId:guid}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ExamScoreDto>>>> GetByTroop(
        Guid troopId, [FromQuery] int? year)
    {
        var result = await _service.GetByTroopAsync(troopId, year);
        return Ok(ApiResponse<IEnumerable<ExamScoreDto>>.Ok(result));
    }

    /// <summary>Create or update an exam score (upserts by memberId+year).</summary>
    [HttpPost]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<ExamScoreDto>>> Create([FromBody] CreateExamScoreDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return Ok(ApiResponse<ExamScoreDto>.Ok(result, "Exam score saved"));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<ExamScoreDto>>> Update(Guid id, [FromBody] UpdateExamScoreDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return result is null
            ? NotFound(ApiResponse.Fail("Exam score not found"))
            : Ok(ApiResponse<ExamScoreDto>.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        var ok = await _service.DeleteAsync(id);
        return ok ? Ok(ApiResponse.Ok("Deleted")) : NotFound(ApiResponse.Fail("Not found"));
    }
}
