using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Points;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/member-point-categories")]
[Authorize]
public class MemberPointCategoriesController : ControllerBase
{
    private readonly IPointsService _service;

    public MemberPointCategoriesController(IPointsService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<PointCategoryDto>>>> GetAll([FromQuery] Guid? groupId)
    {
        var result = await _service.GetMemberCategoriesAsync(groupId);
        return Ok(ApiResponse<IEnumerable<PointCategoryDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<PointCategoryDto>>> Create([FromBody] CreatePointCategoryDto dto)
    {
        var result = await _service.CreateMemberCategoryAsync(dto);
        return Ok(ApiResponse<PointCategoryDto>.Ok(result, "Member point category created"));
    }
}
