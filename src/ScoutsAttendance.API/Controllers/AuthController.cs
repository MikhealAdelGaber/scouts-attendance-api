using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Auth;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<TokenResponseDto>>> Login([FromBody] LoginDto dto)
    {
        var result = await _auth.LoginAsync(dto);
        if (result is null) return Unauthorized(ApiResponse.Fail("Invalid credentials"));
        return Ok(ApiResponse<TokenResponseDto>.Ok(result, "Login successful"));
    }

    [HttpPost("register")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<TokenResponseDto>>> Register([FromBody] RegisterDto dto)
    {
        var result = await _auth.RegisterAsync(dto);
        if (result is null) return BadRequest(ApiResponse.Fail("Username or email already exists"));
        return Ok(ApiResponse<TokenResponseDto>.Ok(result, "User created"));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse>> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = GetUserId();
        var ok = await _auth.ChangePasswordAsync(userId, dto.CurrentPassword, dto.NewPassword);
        return ok ? Ok(ApiResponse.Ok("Password changed")) : BadRequest(ApiResponse.Fail("Incorrect current password"));
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(claim))
            throw new UnauthorizedAccessException("User ID claim not found");
        return Guid.Parse(claim);
    }
}

public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
