using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Points;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _service;

    public LeaderboardController(ILeaderboardService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<LeaderboardDto>>> GetLeaderboard([FromQuery] Guid? groupId)
    {
        var result = await _service.GetLeaderboardAsync(groupId);
        return Ok(ApiResponse<LeaderboardDto>.Ok(result));
    }

    [HttpGet("troops")]
    public async Task<ActionResult<ApiResponse<List<TroopRankingDto>>>> GetTroopRankings([FromQuery] Guid? groupId)
    {
        var result = await _service.GetTroopRankingsAsync(groupId);
        return Ok(ApiResponse<List<TroopRankingDto>>.Ok(result));
    }

    [HttpGet("members")]
    public async Task<ActionResult<ApiResponse<List<MemberRankingDto>>>> GetMemberRankings(
        [FromQuery] Guid? groupId,
        [FromQuery] Guid? troopId,
        [FromQuery] int top = 50)
    {
        var result = await _service.GetMemberRankingsAsync(groupId, troopId, top);
        return Ok(ApiResponse<List<MemberRankingDto>>.Ok(result));
    }
}
