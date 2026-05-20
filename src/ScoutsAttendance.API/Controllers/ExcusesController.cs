using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Excuses;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SystemAdmin,GroupLeader,AttendanceOnly")]
public class ExcusesController : ControllerBase
{
    private readonly IExcuseService _service;
    public ExcusesController(IExcuseService service) => _service = service;

    [HttpGet("member/{memberId:guid}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<MemberExcuseDto>>>> GetByMember(Guid memberId)
    {
        var result = await _service.GetByMemberAsync(memberId);
        return Ok(ApiResponse<IEnumerable<MemberExcuseDto>>.Ok(result));
    }

    [HttpGet("active")]   // keep old route so existing clients don't break
    [HttpGet]             // also expose as GET /api/excuses
    public async Task<ActionResult<ApiResponse<IEnumerable<MemberExcuseDto>>>> GetAll([FromQuery] Guid? troopId)
    {
        var result = await _service.GetAllAsync(troopId);
        return Ok(ApiResponse<IEnumerable<MemberExcuseDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<MemberExcuseDto>>> Grant([FromBody] GrantExcuseDto dto)
    {
        try
        {
            var result = await _service.GrantAsync(dto);
            return Ok(ApiResponse<MemberExcuseDto>.Ok(result, "Excuse granted"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            // Surface the real database / service error so the client can report it.
            // Use the innermost exception message which typically contains the
            // PostgreSQL error detail (e.g. "relation MemberExcuses does not exist").
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException;
            var detail = inner.Message.Length > 300 ? inner.Message[..300] : inner.Message;
            return StatusCode(500, ApiResponse.Fail($"Failed to save excuse: {detail}"));
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<MemberExcuseDto>>> Update(Guid id, [FromBody] UpdateExcuseDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return result is null
            ? NotFound(ApiResponse.Fail("Excuse not found"))
            : Ok(ApiResponse<MemberExcuseDto>.Ok(result));
    }

    /// <summary>Revoke an excuse — restricted to GroupLeader and SystemAdmin only.</summary>
    [HttpDelete("{id:guid}/revoke")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse>> Revoke(Guid id)
    {
        var ok = await _service.RevokeAsync(id);
        return ok ? Ok(ApiResponse.Ok("Excuse revoked")) : NotFound(ApiResponse.Fail("Excuse not found"));
    }
}
