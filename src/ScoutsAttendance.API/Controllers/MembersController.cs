using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Members;
using ScoutsAttendance.Application.DTOs.Transfers;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SystemAdmin,GroupLeader,AttendanceOnly")]
public class MembersController : ControllerBase
{
    private readonly IMemberService       _service;
    private readonly ITransferService     _transfer;
    private readonly IMemberImportService _import;
    private readonly ICurrentUserService  _currentUser;
    private readonly IQrPdfExportService  _qrPdf;

    public MembersController(
        IMemberService service,
        ITransferService transfer,
        IMemberImportService import,
        ICurrentUserService currentUser,
        IQrPdfExportService qrPdf)
    {
        _service     = service;
        _transfer    = transfer;
        _import      = import;
        _currentUser = currentUser;
        _qrPdf       = qrPdf;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<MemberDto>>>> GetAll(
        [FromQuery] Guid?   groupId        = null,
        [FromQuery] Guid?   troopId        = null,
        [FromQuery] int     page           = 1,
        [FromQuery] int     pageSize       = 20,
        [FromQuery] string? search         = null,
        [FromQuery] string? academicYear   = null,
        [FromQuery] string? region         = null,
        [FromQuery] bool?   hasNeckerchief = null,
        [FromQuery] bool?   unassigned     = null)
    {
        var result = await _service.GetAllAsync(groupId, troopId, page, pageSize, search, academicYear, region, hasNeckerchief, unassigned);
        return Ok(ApiResponse<PagedResult<MemberDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<MemberDto>>> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null ? NotFound(ApiResponse.Fail("Member not found")) : Ok(ApiResponse<MemberDto>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<MemberDto>>> Create([FromBody] CreateMemberDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<MemberDto>.Ok(result, "Member created"));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<MemberDto>>> Update(Guid id, [FromBody] UpdateMemberDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return result is null ? NotFound(ApiResponse.Fail("Member not found")) : Ok(ApiResponse<MemberDto>.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        var ok = await _service.DeleteAsync(id);
        return ok ? Ok(ApiResponse.Ok("Member deleted")) : NotFound(ApiResponse.Fail("Member not found"));
    }

    [HttpGet("{id:guid}/qrcode")]
    public async Task<IActionResult> GetQrCode(Guid id)
    {
        var image = await _service.GetQrCodeImageAsync(id);
        return File(image, "image/png");
    }

    [HttpGet("{id:guid}/transfers")]
    public async Task<ActionResult<ApiResponse<IEnumerable<TransferDto>>>> GetTransfers(Guid id)
    {
        var result = await _transfer.GetMemberTransfersAsync(id);
        return Ok(ApiResponse<IEnumerable<TransferDto>>.Ok(result));
    }

    [HttpPost("transfer")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<TransferDto>>> Transfer([FromBody] CreateTransferDto dto)
    {
        var result = await _transfer.CreateTransferAsync(dto);
        return Ok(ApiResponse<TransferDto>.Ok(result, "Member transferred successfully"));
    }

    /// <summary>Bulk update talaea / academic year / grade for all members in a troop at year start.</summary>
    [HttpPost("bulk-year-update")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<int>>> BulkYearUpdate([FromBody] BulkYearUpdateDto dto)
    {
        var count = await _service.BulkYearUpdateAsync(dto);
        return Ok(ApiResponse<int>.Ok(count, $"Updated {count} member(s)"));
    }

    /// <summary>Downloads a pre-formatted .xlsx template for bulk member import.</summary>
    [HttpGet("import-template")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public IActionResult DownloadImportTemplate()
    {
        var bytes = _import.GenerateTemplate();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "members_import_template.xlsx");
    }

    /// <summary>
    /// Generates a print-ready A4 PDF containing all member QR codes, grouped
    /// by Troop (3 cards per row).  Scoping is automatic:
    ///   SystemAdmin  → all troops &amp; all members
    ///   GroupLeader  → own group's members
    /// </summary>
    [HttpGet("export-qr-pdf")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<IActionResult> ExportQrCodesPdf()
    {
        var (bytes, filename) = await _qrPdf.ExportAsync();
        return File(bytes, "application/pdf", filename);
    }

    /// <summary>
    /// Uploads a filled-in .xlsx file and bulk-imports valid member rows.
    /// Pass troopId if the current user is not automatically scoped to a troop.
    /// </summary>
    [HttpPost("import")]
    [Authorize(Roles = "SystemAdmin,GroupLeader")]
    public async Task<ActionResult<ApiResponse<ImportMembersResultDto>>> ImportMembers(
        IFormFile file,
        [FromQuery] Guid? troopId = null)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse.Fail("No file uploaded"));

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(ApiResponse.Fail("File exceeds 5 MB limit"));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx")
            return BadRequest(ApiResponse.Fail("Only .xlsx files are accepted"));

        // Resolve troop: caller param > user's own troop > error
        var effectiveTroopId = troopId
            ?? (_currentUser.HasTroopScope ? _currentUser.TroopId : null);

        if (!effectiveTroopId.HasValue)
            return BadRequest(ApiResponse.Fail(
                "Please select a troop from the Filters panel before importing, then try again."));

        using var stream = file.OpenReadStream();
        var result = await _import.ImportAsync(stream, effectiveTroopId.Value);

        var message = result.ImportedCount == 0
            ? $"No members imported. {result.SkippedCount} row(s) skipped."
            : $"{result.ImportedCount} member(s) imported successfully. {result.SkippedCount} row(s) skipped.";

        return Ok(ApiResponse<ImportMembersResultDto>.Ok(result, message));
    }
}
