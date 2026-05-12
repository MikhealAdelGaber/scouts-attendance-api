using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Users;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]   // base: any authenticated user; individual endpoints lock down further
public class UsersController : ControllerBase
{
    private readonly IUserManagementService _service;

    public UsersController(IUserManagementService service) => _service = service;

    /// <summary>List all users (SystemAdmin only).</summary>
    [HttpGet]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<ActionResult<ApiResponse<IEnumerable<UserDto>>>> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<IEnumerable<UserDto>>.Ok(result));
    }

    /// <summary>Get a single user by ID (SystemAdmin only).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetById(Guid id)
    {
        var user = await _service.GetByIdAsync(id);
        return user is null
            ? NotFound(ApiResponse<UserDto>.Fail("User not found"))
            : Ok(ApiResponse<UserDto>.Ok(user));
    }

    /// <summary>Create a new user (SystemAdmin only).</summary>
    [HttpPost]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Create([FromBody] CreateUserDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return Ok(ApiResponse<UserDto>.Ok(result, "User created successfully"));
    }

    /// <summary>Update role, troop assignment and permission flags (SystemAdmin only).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update(Guid id, [FromBody] UpdateUserDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return result is null
            ? NotFound(ApiResponse<UserDto>.Fail("User not found"))
            : Ok(ApiResponse<UserDto>.Ok(result, "User updated"));
    }

    /// <summary>Soft-delete a user (SystemAdmin only).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        var ok = await _service.DeleteAsync(id);
        return ok
            ? Ok(ApiResponse.Ok("User deleted"))
            : NotFound(ApiResponse.Fail("User not found"));
    }

    /// <summary>
    /// Returns users eligible to be assigned as a troop/group leader.
    /// Open to any authenticated user so leader dropdowns work on Troop/Group forms.
    /// </summary>
    [HttpGet("leaders")]
    public async Task<ActionResult<ApiResponse<IEnumerable<UserLeaderDto>>>> GetLeaders()
    {
        var result = await _service.GetAvailableLeadersAsync();
        return Ok(ApiResponse<IEnumerable<UserLeaderDto>>.Ok(result));
    }
}
