using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.PendingExcuses;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

/// <summary>
/// Authenticated endpoints for GroupLeaders, SystemAdmins, and AttendanceOnly users
/// to manage pending excuse submissions made via the public shareable link.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SystemAdmin,GroupLeader,AttendanceOnly,GroupLeaderAdmin")]
public class PendingExcusesController : ControllerBase
{
    private readonly IPendingExcuseService _service;
    public PendingExcusesController(IPendingExcuseService service) => _service = service;

    /// <summary>Returns all pending (unreviewed) excuse submissions visible to the caller.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<PendingExcuseDto>>>> GetPending(
        [FromQuery] Guid? troopId)
    {
        var result = await _service.GetPendingAsync(troopId);
        return Ok(ApiResponse<IEnumerable<PendingExcuseDto>>.Ok(result));
    }

    /// <summary>Returns the count of pending excuse submissions (used for the nav badge).</summary>
    [HttpGet("count")]
    public async Task<ActionResult<ApiResponse<int>>> GetPendingCount([FromQuery] Guid? groupId)
    {
        var count = await _service.GetPendingCountAsync(groupId);
        return Ok(ApiResponse<int>.Ok(count));
    }

    /// <summary>Approves a pending excuse, creating the real MemberExcuse record.</summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ApiResponse<PendingExcuseDto>>> Approve(
        Guid id,
        [FromBody] ReviewNotesDto dto)
    {
        try
        {
            var result = await _service.ApproveAsync(id, dto.ReviewNotes);
            if (result is null)
                return NotFound(ApiResponse<PendingExcuseDto>.Fail("Pending excuse not found."));

            return Ok(ApiResponse<PendingExcuseDto>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<PendingExcuseDto>.Fail(ex.Message));
        }
    }

    /// <summary>Rejects a pending excuse submission.</summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ApiResponse<PendingExcuseDto>>> Reject(
        Guid id,
        [FromBody] ReviewNotesDto dto)
    {
        try
        {
            var result = await _service.RejectAsync(id, dto.ReviewNotes);
            if (result is null)
                return NotFound(ApiResponse<PendingExcuseDto>.Fail("Pending excuse not found."));

            return Ok(ApiResponse<PendingExcuseDto>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<PendingExcuseDto>.Fail(ex.Message));
        }
    }
}
