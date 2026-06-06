using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _service;
    public ProfileController(IProfileService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<ScoutsAttendance.Application.Services.ProfileDto>>> GetProfile()
    {
        var result = await _service.GetCurrentAsync();
        return result is null
            ? NotFound(ApiResponse.Fail("Profile not found"))
            : Ok(ApiResponse<ScoutsAttendance.Application.Services.ProfileDto>.Ok(result));
    }

    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse>> ChangePassword([FromBody] ScoutsAttendance.Application.Services.ChangePasswordDto dto)
    {
        var ok = await _service.ChangePasswordAsync(dto);
        return ok ? Ok(ApiResponse.Ok("Password changed successfully")) : BadRequest(ApiResponse.Fail("Failed to change password"));
    }

    /// <summary>
    /// POST /api/profile/verify-password
    /// Verifies the caller's own password — used by security dialogs before destructive actions.
    /// Accessible to ALL authenticated roles (not just SystemAdmin).
    /// </summary>
    [HttpPost("verify-password")]
    public async Task<ActionResult<ApiResponse>> VerifyPassword([FromBody] VerifyPasswordBodyDto dto)
    {
        var ok = await _service.VerifyCurrentPasswordAsync(dto.Password);
        return ok
            ? Ok(ApiResponse.Ok("Password verified"))
            : BadRequest(ApiResponse.Fail("Incorrect password. Please try again."));
    }
}

public record VerifyPasswordBodyDto(string Password);
