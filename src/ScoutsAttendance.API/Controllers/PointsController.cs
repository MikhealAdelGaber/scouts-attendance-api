using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Points;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PointsController : ControllerBase
{
    private readonly IPointsService _service;

    public PointsController(IPointsService service) => _service = service;

    // ─── Attendance Settings ──────────────────────────────────────────────────

    [HttpGet("attendance-settings")]
    public async Task<ActionResult<ApiResponse<PointCategoryDto>>> GetAttendanceSettings()
    {
        var result = await _service.GetAttendanceCategoryAsync();
        return result is null
            ? NotFound(ApiResponse.Fail("Attendance category not found"))
            : Ok(ApiResponse<PointCategoryDto>.Ok(result));
    }

    /// <summary>Update attendance point values — admin / leader only.</summary>
    [HttpPut("attendance-settings/{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<PointCategoryDto>>> UpdateAttendanceSettings(
        Guid id, [FromBody] UpdateAttendancePointsDto dto)
    {
        var result = await _service.UpdateAttendanceCategoryAsync(id, dto);
        return result is null
            ? NotFound(ApiResponse.Fail("Attendance category not found"))
            : Ok(ApiResponse<PointCategoryDto>.Ok(result, "Attendance points settings saved"));
    }

    // ─── Member Points ────────────────────────────────────────────────────────

    [HttpGet("members/{memberId:guid}")]
    public async Task<ActionResult<ApiResponse<MemberPointsSummaryDto>>> GetMemberPoints(Guid memberId)
    {
        var result = await _service.GetMemberPointsAsync(memberId);
        return Ok(ApiResponse<MemberPointsSummaryDto>.Ok(result));
    }

    /// <summary>Award points to a member — admin, leader, or attendance user.</summary>
    [HttpPost("members")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,AttendanceOnly")]
    public async Task<ActionResult<ApiResponse<MemberPointsDto>>> AddMemberPoints([FromBody] AddMemberPointsDto dto)
    {
        var result = await _service.AddMemberPointsAsync(dto);
        return Ok(ApiResponse<MemberPointsDto>.Ok(result, "Points added"));
    }

    /// <summary>Remove a member points record — admin, leader, or attendance user.</summary>
    [HttpDelete("members/{pointsId:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,AttendanceOnly")]
    public async Task<ActionResult<ApiResponse>> DeleteMemberPoints(Guid pointsId)
    {
        var ok = await _service.DeleteMemberPointsAsync(pointsId);
        return ok ? Ok(ApiResponse.Ok("Points deleted")) : NotFound(ApiResponse.Fail("Points record not found"));
    }

    // ─── Troop Points ─────────────────────────────────────────────────────────

    [HttpGet("troops/{troopId:guid}")]
    public async Task<ActionResult<ApiResponse<TroopPointsSummaryDto>>> GetTroopPoints(Guid troopId)
    {
        var result = await _service.GetTroopPointsAsync(troopId);
        return Ok(ApiResponse<TroopPointsSummaryDto>.Ok(result));
    }

    /// <summary>Award points to a troop — admin, leader, or attendance user.</summary>
    [HttpPost("troops")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,AttendanceOnly")]
    public async Task<ActionResult<ApiResponse<TroopPointsDto>>> AddTroopPoints([FromBody] AddTroopPointsDto dto)
    {
        var result = await _service.AddTroopPointsAsync(dto);
        return Ok(ApiResponse<TroopPointsDto>.Ok(result, "Points added"));
    }

    /// <summary>Remove a troop points record — admin, leader, or attendance user.</summary>
    [HttpDelete("troops/{pointsId:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,AttendanceOnly")]
    public async Task<ActionResult<ApiResponse>> DeleteTroopPoints(Guid pointsId)
    {
        var ok = await _service.DeleteTroopPointsAsync(pointsId);
        return ok ? Ok(ApiResponse.Ok("Points deleted")) : NotFound(ApiResponse.Fail("Points record not found"));
    }
}
