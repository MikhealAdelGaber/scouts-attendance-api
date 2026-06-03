using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Badges;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/badges")]
[Authorize]
public class BadgesController : ControllerBase
{
    private readonly IBadgeService       _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IExcelExportService _excel;

    public BadgesController(IBadgeService service, ICurrentUserService currentUser, IExcelExportService excel)
    {
        _service     = service;
        _excel       = excel;
        _currentUser = currentUser;
    }

    // ── Catalog ───────────────────────────────────────────────────────────────

    /// <summary>GET /api/badges — all badges in catalog (anyone who can access badges).</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<BadgeDto>>>> GetAll()
    {
        if (!_currentUser.CanAccessBadges)
            return Forbid();

        var result = await _service.GetAllBadgesAsync();
        return Ok(ApiResponse<IEnumerable<BadgeDto>>.Ok(result));
    }

    /// <summary>GET /api/badges/{id}</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<BadgeDto>>> GetById(Guid id)
    {
        if (!_currentUser.CanAccessBadges)
            return Forbid();

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
        if (!_currentUser.CanAccessBadges)
            return Forbid();

        var result = await _service.GetMemberBadgesAsync(memberId);
        return Ok(ApiResponse<IEnumerable<MemberBadgeDto>>.Ok(result));
    }

    /// <summary>POST /api/badges/member/{memberId} — award badge.
    /// Allowed: SystemAdmin, GroupLeader, or any user with canAccessBadges claim.</summary>
    [HttpPost("member/{memberId:guid}")]
    public async Task<ActionResult<ApiResponse<MemberBadgeDto>>> Award(Guid memberId, [FromBody] AwardBadgeDto dto)
    {
        if (!_currentUser.CanAccessBadges)
            return Forbid();

        try
        {
            var result = await _service.AwardBadgeAsync(memberId, dto);
            return Ok(ApiResponse<MemberBadgeDto>.Ok(result, "Badge awarded"));
        }
        catch (InvalidOperationException ex)
        {
            // 409 = duplicate badge for this member
            return Conflict(ApiResponse.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>DELETE /api/badges/member/{memberId}/{memberBadgeId} — remove award.
    /// Allowed: SystemAdmin or GroupLeader only.</summary>
    [HttpDelete("member/{memberId:guid}/{memberBadgeId:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse>> Remove(Guid memberId, Guid memberBadgeId)
    {
        var ok = await _service.RemoveMemberBadgeAsync(memberId, memberBadgeId);
        return ok
            ? Ok(ApiResponse.Ok("Badge removed"))
            : NotFound(ApiResponse.Fail("Award record not found"));
    }

    // ── Activity Feed ─────────────────────────────────────────────────────────

    /// <summary>GET /api/badges/recent?limit=30 — latest awarded badges in the user's group.</summary>
    [HttpGet("recent")]
    public async Task<ActionResult<ApiResponse<IEnumerable<MemberBadgeDto>>>> GetRecent([FromQuery] int limit = 30)
    {
        if (!_currentUser.CanAccessBadges)
            return Forbid();

        if (limit < 1 || limit > 100) limit = 30;
        var result = await _service.GetRecentBadgesAsync(limit);
        return Ok(ApiResponse<IEnumerable<MemberBadgeDto>>.Ok(result));
    }

    // ── Excel Export ──────────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/badges/export?troopId=&amp;category=&amp;from=&amp;to=
    /// Downloads an Excel file of badge awards with the given filters applied.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] Guid?    troopId  = null,
        [FromQuery] string?  category = null,
        [FromQuery] DateTime? from    = null,
        [FromQuery] DateTime? to      = null)
    {
        if (!_currentUser.CanAccessBadges)
            return Forbid();

        var bytes    = await _excel.ExportBadgesAsync(troopId, category, from, to);
        var filename = $"Badges-Report-{DateTime.UtcNow:yyyy-MM-dd}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            filename);
    }
}
