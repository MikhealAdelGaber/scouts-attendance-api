using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Users;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Application.Services;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]   // base: any authenticated user; individual endpoints lock down further
public class UsersController : ControllerBase
{
    private readonly IUserManagementService _service;
    private readonly IGroupUserManagementService _groupUserService;

    public UsersController(IUserManagementService service, IGroupUserManagementService groupUserService)
    {
        _service          = service;
        _groupUserService = groupUserService;
    }

    private Guid CurrentUserId => Guid.Parse(
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

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

    /// <summary>Admin changes any user's password (no old password required).</summary>
    [HttpPost("{id:guid}/change-password")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<ActionResult<ApiResponse>> AdminChangePassword(Guid id, [FromBody] AdminChangePasswordDto dto)
    {
        var ok = await _service.AdminChangePasswordAsync(id, dto.NewPassword);
        return ok
            ? Ok(ApiResponse.Ok("Password changed successfully"))
            : NotFound(ApiResponse.Fail("User not found"));
    }

    /// <summary>Toggles a user's IsActive flag. Cannot be used on own account.</summary>
    [HttpPost("{id:guid}/toggle-status")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<ActionResult<ApiResponse<UserDto>>> ToggleStatus(Guid id)
    {
        try
        {
            var result = await _service.ToggleStatusAsync(id, CurrentUserId);
            return result is null
                ? NotFound(ApiResponse<UserDto>.Fail("User not found"))
                : Ok(ApiResponse<UserDto>.Ok(result,
                    result.IsActive ? "User activated" : "User deactivated"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<UserDto>.Fail(ex.Message));
        }
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

    // ─── GroupLeaderAdmin endpoints (NEW — do not modify above) ──────────────

    /// <summary>
    /// Returns users in the same group as the caller.
    /// GroupLeaderAdmin: scoped to their own group only.
    /// SystemAdmin: sees all users.
    /// </summary>
    [HttpGet("group-users")]
    [Authorize(Roles = "GroupLeaderAdmin,SystemAdmin")]
    public async Task<ActionResult<ApiResponse<IEnumerable<GroupUserDto>>>> GetGroupUsers()
    {
        var result = await _groupUserService.GetGroupUsersAsync();
        return Ok(ApiResponse<IEnumerable<GroupUserDto>>.Ok(result));
    }

    /// <summary>
    /// GroupLeaderAdmin updates role + permissions of a user in their own group.
    /// Cannot assign SystemAdmin or GroupLeaderAdmin roles.
    /// Cannot edit own role.
    /// Returns 403 if the target is outside the caller's group.
    /// </summary>
    [HttpPut("{id:guid}/update-role-permissions")]
    [Authorize(Roles = "GroupLeaderAdmin,SystemAdmin")]
    public async Task<ActionResult<ApiResponse<GroupUserDto>>> UpdateRolePermissions(
        Guid id,
        [FromBody] UpdateRolePermissionsDto dto)
    {
        try
        {
            var result = await _groupUserService.UpdateRolePermissionsAsync(id, dto, CurrentUserId);
            return result is null
                ? NotFound(ApiResponse<GroupUserDto>.Fail("User not found"))
                : Ok(ApiResponse<GroupUserDto>.Ok(result, "User updated successfully"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<GroupUserDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<GroupUserDto>.Fail(ex.Message));
        }
    }
}
