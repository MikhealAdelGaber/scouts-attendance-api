using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Points;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/troop-point-categories")]
[Authorize]
public class TroopPointCategoriesController : ControllerBase
{
    private readonly IPointsService _service;

    public TroopPointCategoriesController(IPointsService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<PointCategoryDto>>>> GetAll([FromQuery] Guid? groupId)
    {
        var result = await _service.GetTroopCategoriesAsync(groupId);
        return Ok(ApiResponse<IEnumerable<PointCategoryDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<PointCategoryDto>>> Create([FromBody] CreatePointCategoryDto dto)
    {
        var result = await _service.CreateTroopCategoryAsync(dto);
        return Ok(ApiResponse<PointCategoryDto>.Ok(result, "Troop point category created"));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        var (ok, error) = await _service.DeleteTroopCategoryAsync(id);
        if (!ok) return Conflict(ApiResponse.Fail(error));
        return Ok(ApiResponse.Ok("Category deleted"));
    }
}
