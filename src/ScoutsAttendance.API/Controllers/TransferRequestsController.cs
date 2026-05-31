using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.TransferRequests;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/transfer-requests")]
[Authorize]
public class TransferRequestsController : ControllerBase
{
    private readonly ITransferRequestService _service;
    private readonly ICurrentUserService     _currentUser;

    public TransferRequestsController(ITransferRequestService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    // ── Create ────────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/transfer-requests — create a new pending transfer request.
    /// Allowed: GroupLeader (own group members) or SystemAdmin.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<TransferRequestDto>>> Create(
        [FromBody] CreateTransferRequestDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return Ok(ApiResponse<TransferRequestDto>.Ok(result, "Transfer request created"));
        }
        catch (KeyNotFoundException ex)    { return NotFound(ApiResponse.Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse.Fail(ex.Message)); }
        catch (InvalidOperationException ex)   { return BadRequest(ApiResponse.Fail(ex.Message)); }
    }

    // ── List ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/transfer-requests — list requests.
    /// SystemAdmin sees all; GroupLeader sees own group's (from/to).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TransferRequestDto>>>> GetList()
    {
        var result = await _service.GetListAsync();
        return Ok(ApiResponse<IEnumerable<TransferRequestDto>>.Ok(result));
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    /// <summary>
    /// PUT /api/transfer-requests/{id}/approve — approve and execute the transfer.
    /// SystemAdmin only.
    /// </summary>
    [HttpPut("{id:guid}/approve")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<ActionResult<ApiResponse>> Approve(Guid id)
    {
        var (ok, error) = await _service.ApproveAsync(id);
        return ok
            ? Ok(ApiResponse.Ok("Transfer approved and member moved to new group"))
            : BadRequest(ApiResponse.Fail(error));
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    /// <summary>
    /// PUT /api/transfer-requests/{id}/reject — reject with optional reason.
    /// SystemAdmin only.
    /// </summary>
    [HttpPut("{id:guid}/reject")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<ActionResult<ApiResponse>> Reject(
        Guid id, [FromBody] ReviewTransferRequestDto dto)
    {
        var (ok, error) = await _service.RejectAsync(id, dto);
        return ok
            ? Ok(ApiResponse.Ok("Transfer request rejected"))
            : BadRequest(ApiResponse.Fail(error));
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    /// <summary>
    /// DELETE /api/transfer-requests/{id} — cancel a pending request.
    /// GroupLeader (own group) or SystemAdmin.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse>> Cancel(Guid id)
    {
        var (ok, error) = await _service.CancelAsync(id);
        return ok
            ? Ok(ApiResponse.Ok("Transfer request cancelled"))
            : BadRequest(ApiResponse.Fail(error));
    }

    // ── Member history ────────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/members/{memberId}/transfer-history — approved transfer history for a member.
    /// </summary>
    [HttpGet("/api/members/{memberId:guid}/transfer-history")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TransferRequestDto>>>> GetMemberHistory(Guid memberId)
    {
        var result = await _service.GetMemberHistoryAsync(memberId);
        return Ok(ApiResponse<IEnumerable<TransferRequestDto>>.Ok(result));
    }

    // ── Pending count (nav badge) ─────────────────────────────────────────────

    /// <summary>
    /// GET /api/transfer-requests/pending-count — count of pending requests visible to the user.
    /// </summary>
    [HttpGet("pending-count")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<int>>> GetPendingCount()
    {
        var count = await _service.GetPendingCountAsync();
        return Ok(ApiResponse<int>.Ok(count));
    }
}
