using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Badges;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/badges")]
[Authorize]
public class BadgesController : ControllerBase
{
    private readonly IBadgeService _service;

    public BadgesController(IBadgeService service) => _service = service;

    // ── Catalog ───────────────────────────────────────────────────────────────

    /// <summary>GET /api/badges — all badges in catalog (all roles).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<BadgeDto>>>> GetAll()
    {
        var result = await _service.GetAllBadgesAsync();
        return Ok(ApiResponse<IEnumerable<BadgeDto>>.Ok(result));
    }

    /// <summary>GET /api/badges/{id}</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<BadgeDto>>> GetById(Guid id)
    {
        var result = await _service.GetBadgeByIdAsync(id);
        return result is null
            ? NotFound(ApiResponse.Fail("Badge not found"))
            : Ok(ApiResponse<BadgeDto>.Ok(result));
    }

    /// <summary>POST /api/badges — create badge (SystemAdmin only).</summary>
    [HttpPost]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<ActionResult<ApiResponse<BadgeDto>>> Create([FromBody] CreateBadgeDto dto)
    {
        var result = await _service.CreateBadgeAsync(dto);
        return Ok(ApiResponse<BadgeDto>.Ok(result, "Badge created"));
    }

    /// <summary>PUT /api/badges/{id} — update badge (SystemAdmin only).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<ActionResult<ApiResponse<BadgeDto>>> Update(Guid id, [FromBody] UpdateBadgeDto dto)
    {
        var result = await _service.UpdateBadgeAsync(id, dto);
        return result is null
            ? NotFound(ApiResponse.Fail("Badge not found"))
            : Ok(ApiResponse<BadgeDto>.Ok(result, "Badge updated"));
    }

    /// <summary>DELETE /api/badges/{id} — delete badge (SystemAdmin only, only if not awarded).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        var (ok, error) = await _service.DeleteBadgeAsync(id);
        return ok
            ? Ok(ApiResponse.Ok("Badge deleted"))
            : BadRequest(ApiResponse.Fail(error));
    }

    // ── Member Badges ─────────────────────────────────────────────────────────

    /// <summary>GET /api/badges/member/{memberId} — badges awarded to a member.</summary>
    [HttpGet("member/{memberId:guid}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<MemberBadgeDto>>>> GetMemberBadges(Guid memberId)
    {
        var result = await _service.GetMemberBadgesAsync(memberId);
        return Ok(ApiResponse<IEnumerable<MemberBadgeDto>>.Ok(result));
    }

    /// <summary>POST /api/badges/member/{memberId} — award badge (GroupLeader, SystemAdmin, AttendanceOnly).</summary>
    [HttpPost("member/{memberId:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,AttendanceOnly")]
    public async Task<ActionResult<ApiResponse<MemberBadgeDto>>> Award(Guid memberId, [FromBody] AwardBadgeDto dto)
    {
        try
        {
            var result = await _service.AwardBadgeAsync(memberId, dto);
            return Ok(ApiResponse<MemberBadgeDto>.Ok(result, "Badge awarded"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>DELETE /api/badges/member/{memberId}/{memberBadgeId} — remove award (GroupLeader, SystemAdmin).</summary>
    [HttpDelete("member/{memberId:guid}/{memberBadgeId:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse>> Remove(Guid memberId, Guid memberBadgeId)
    {
        var ok = await _service.RemoveMemberBadgeAsync(memberId, memberBadgeId);
        return ok
            ? Ok(ApiResponse.Ok("Badge removed"))
            : NotFound(ApiResponse.Fail("Award record not found"));
    }
}
