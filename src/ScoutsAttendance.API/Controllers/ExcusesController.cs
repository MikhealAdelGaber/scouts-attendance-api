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

    [HttpGet("active")]
    public async Task<ActionResult<ApiResponse<IEnumerable<MemberExcuseDto>>>> GetAllActive([FromQuery] Guid? troopId)
    {
        var result = await _service.GetAllActiveAsync(troopId);
        return Ok(ApiResponse<IEnumerable<MemberExcuseDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<MemberExcuseDto>>> Grant([FromBody] GrantExcuseDto dto)
    {
        var result = await _service.GrantAsync(dto);
        return Ok(ApiResponse<MemberExcuseDto>.Ok(result, "Excuse granted"));
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
