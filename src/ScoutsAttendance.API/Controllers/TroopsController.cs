using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Troops;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TroopsController : ControllerBase
{
    private readonly ITroopService _service;

    public TroopsController(ITroopService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<TroopDto>>>> GetAll([FromQuery] Guid? groupId)
    {
        var result = await _service.GetAllAsync(groupId);
        return Ok(ApiResponse<IEnumerable<TroopDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<TroopDto>>> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null ? NotFound(ApiResponse.Fail("Troop not found")) : Ok(ApiResponse<TroopDto>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse<TroopDto>>> Create([FromBody] CreateTroopDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<TroopDto>.Ok(result, "Troop created"));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse<TroopDto>>> Update(Guid id, [FromBody] UpdateTroopDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return result is null ? NotFound(ApiResponse.Fail("Troop not found")) : Ok(ApiResponse<TroopDto>.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        var ok = await _service.DeleteAsync(id);
        return ok ? Ok(ApiResponse.Ok("Troop deleted")) : NotFound(ApiResponse.Fail("Troop not found"));
    }

    /// <summary>
    /// Generates a new ShareToken for the troop, immediately invalidating the old public link.
    /// Only GroupLeader and SystemAdmin can reset a token.
    /// </summary>
    [HttpPost("{id:guid}/reset-token")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse<string>>> ResetToken(Guid id)
    {
        var newToken = await _service.ResetShareTokenAsync(id);
        return newToken is null
            ? NotFound(ApiResponse<string>.Fail("Troop not found"))
            : Ok(ApiResponse<string>.Ok(newToken, "Share token reset successfully"));
    }
}
