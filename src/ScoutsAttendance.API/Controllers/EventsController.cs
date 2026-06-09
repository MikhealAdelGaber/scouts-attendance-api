using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Events;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly IEventService _service;

    public EventsController(IEventService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<EventDto>>>> GetAll(
        [FromQuery] Guid? groupId, [FromQuery] Guid? troopId, [FromQuery] bool activeOnly = false)
    {
        var result = await _service.GetAllAsync(groupId, troopId, activeOnly);
        return Ok(ApiResponse<IEnumerable<EventDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<EventDto>>> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null ? NotFound(ApiResponse.Fail("Event not found")) : Ok(ApiResponse<EventDto>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse<EventDto>>> Create([FromBody] CreateEventDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<EventDto>.Ok(result, "Event created"));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse<EventDto>>> Update(Guid id, [FromBody] UpdateEventDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return result is null ? NotFound(ApiResponse.Fail("Event not found")) : Ok(ApiResponse<EventDto>.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        var ok = await _service.DeleteAsync(id);
        return ok ? Ok(ApiResponse.Ok("Event deleted")) : NotFound(ApiResponse.Fail("Event not found"));
    }
}
