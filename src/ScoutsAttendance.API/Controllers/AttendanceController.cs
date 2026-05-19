using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Attendance;
using ScoutsAttendance.Application.Services;
using ScoutsAttendance.API.Hubs;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService          _service;
    private readonly IHubContext<AttendanceHub>  _hub;

    public AttendanceController(IAttendanceService service, IHubContext<AttendanceHub> hub)
    {
        _service = service;
        _hub     = hub;
    }

    [HttpGet("event/{eventId:guid}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AttendanceDto>>>> GetByEvent(Guid eventId)
    {
        var result = await _service.GetByEventAsync(eventId);
        return Ok(ApiResponse<IEnumerable<AttendanceDto>>.Ok(result));
    }

    /// <summary>
    /// Returns ALL members for an event with their effective attendance status.
    /// Members with no record yet get a computed default:
    ///   • Active excuse covering EventDate → Excused
    ///   • Otherwise → Absent
    /// Use this endpoint to build the attendance page so excused members are
    /// pre-filled without requiring the user to manually set each one.
    /// </summary>
    [HttpGet("event/{eventId:guid}/members")]
    public async Task<ActionResult<ApiResponse<IEnumerable<EventMemberStatusDto>>>> GetEventMembers(Guid eventId)
    {
        var result = await _service.GetEventMemberStatusesAsync(eventId);
        return Ok(ApiResponse<IEnumerable<EventMemberStatusDto>>.Ok(result));
    }

    [HttpGet("member/{memberId:guid}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AttendanceDto>>>> GetMemberHistory(Guid memberId)
    {
        var result = await _service.GetMemberHistoryAsync(memberId);
        return Ok(ApiResponse<IEnumerable<AttendanceDto>>.Ok(result));
    }

    [HttpGet("event/{eventId:guid}/summary")]
    public async Task<ActionResult<ApiResponse<AttendanceSummaryDto>>> GetSummary(Guid eventId)
    {
        var result = await _service.GetSummaryAsync(eventId);
        return Ok(ApiResponse<AttendanceSummaryDto>.Ok(result));
    }

    [HttpPost("mark")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,AttendanceOnly")]
    public async Task<ActionResult<ApiResponse<AttendanceDto>>> Mark([FromBody] MarkAttendanceDto dto)
    {
        var result = await _service.MarkAttendanceAsync(dto);
        // Broadcast to all watchers of this event in real-time
        await _hub.Clients.Group($"event-{dto.EventId}").SendAsync("AttendanceUpdated", result);
        return Ok(ApiResponse<AttendanceDto>.Ok(result, "Attendance marked"));
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,AttendanceOnly")]
    public async Task<ActionResult<ApiResponse<IEnumerable<AttendanceDto>>>> BulkMark([FromBody] BulkAttendanceDto dto)
    {
        var result = (await _service.BulkMarkAsync(dto)).ToList();
        // Broadcast all updates
        foreach (var record in result)
            await _hub.Clients.Group($"event-{dto.EventId}").SendAsync("AttendanceUpdated", record);
        return Ok(ApiResponse<IEnumerable<AttendanceDto>>.Ok(result, "Bulk attendance marked"));
    }

    [HttpPost("qr")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,AttendanceOnly")]
    public async Task<ActionResult<ApiResponse<AttendanceDto>>> MarkByQr([FromBody] QrAttendanceDto dto)
    {
        var result = await _service.MarkByQrAsync(dto);
        if (result is null)
            return BadRequest(ApiResponse.Fail("Invalid QR code or member not found"));

        await _hub.Clients.Group($"event-{dto.EventId}").SendAsync("AttendanceUpdated", result);
        return Ok(ApiResponse<AttendanceDto>.Ok(result, "Attendance marked via QR"));
    }
}
