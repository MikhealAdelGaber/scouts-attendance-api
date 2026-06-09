using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Groups;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GroupsController : ControllerBase
{
    private readonly IGroupService _service;

    public GroupsController(IGroupService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<GroupDto>>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<IEnumerable<GroupDto>>.Ok(result));
    }

    /// <summary>
    /// GET /api/groups/all-for-transfer — returns ALL groups in the system, bypassing
    /// the per-user group scoping that GetAll() applies.  Used exclusively by the
    /// "Request Transfer" dialog so a GroupLeader can pick a destination group they
    /// don't belong to.
    /// </summary>
    [HttpGet("all-for-transfer")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse<IEnumerable<GroupDto>>>> GetAllForTransfer()
    {
        var result = await _service.GetAllForTransferAsync();
        return Ok(ApiResponse<IEnumerable<GroupDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<GroupDto>>> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null ? NotFound(ApiResponse.Fail("Group not found")) : Ok(ApiResponse<GroupDto>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<ActionResult<ApiResponse<GroupDto>>> Create([FromBody] CreateGroupDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<GroupDto>.Ok(result, "Group created"));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse<GroupDto>>> Update(Guid id, [FromBody] UpdateGroupDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return result is null ? NotFound(ApiResponse.Fail("Group not found")) : Ok(ApiResponse<GroupDto>.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        var ok = await _service.DeleteAsync(id);
        return ok ? Ok(ApiResponse.Ok("Group deleted")) : NotFound(ApiResponse.Fail("Group not found"));
    }
}
