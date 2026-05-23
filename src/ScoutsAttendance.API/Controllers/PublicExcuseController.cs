using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.PendingExcuses;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

/// <summary>
/// Public (no authentication required) endpoints for excuse submission via
/// a troop's shareable link.  Rate-limited to 10 submissions per hour per IP.
/// </summary>
[ApiController]
[Route("api/public/excuse")]
public class PublicExcuseController : ControllerBase
{
    private const int MaxRequestsPerHour = 10;
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly IPendingExcuseService _service;
    private readonly IIpRateLimiter        _rateLimiter;

    public PublicExcuseController(IPendingExcuseService service, IIpRateLimiter rateLimiter)
    {
        _service     = service;
        _rateLimiter = rateLimiter;
    }

    /// <summary>Returns basic troop info for the given share token so the form
    /// can display the troop name before submission.</summary>
    [HttpGet("{token}")]
    public async Task<ActionResult<ApiResponse<PublicTroopInfoDto>>> GetTroopInfo(string token)
    {
        var info = await _service.GetTroopByTokenAsync(token);
        if (info is null)
            return NotFound(ApiResponse<PublicTroopInfoDto>.Fail("Invalid or expired excuse link."));

        return Ok(ApiResponse<PublicTroopInfoDto>.Ok(info));
    }

    /// <summary>Submits a new excuse for manual review by a leader.</summary>
    [HttpPost("{token}")]
    public async Task<ActionResult<ApiResponse<PendingExcuseDto>>> Submit(
        string token,
        [FromBody] SubmitPendingExcuseDto dto)
    {
        // Resolve client IP (works behind Railway's reverse proxy)
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var forwarded = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            ip = forwarded.Split(',')[0].Trim();

        if (!_rateLimiter.IsAllowed(ip, MaxRequestsPerHour, Window))
            return StatusCode(429, ApiResponse<PendingExcuseDto>.Fail(
                "Too many submissions. Please try again later."));

        try
        {
            var result = await _service.SubmitAsync(token, dto, ip);
            return Ok(ApiResponse<PendingExcuseDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<PendingExcuseDto>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<PendingExcuseDto>.Fail(ex.Message));
        }
    }
}
