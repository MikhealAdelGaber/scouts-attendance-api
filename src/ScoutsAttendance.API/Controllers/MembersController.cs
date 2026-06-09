using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.Common;
using ScoutsAttendance.Application.DTOs.Members;
using ScoutsAttendance.Application.DTOs.Transfers;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Application.Services;
using ScoutsAttendance.Domain.Entities;

namespace ScoutsAttendance.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SystemAdmin,GroupLeader,AttendanceOnly,GroupLeaderAdmin")]
public class MembersController : ControllerBase
{
    private readonly IMemberService       _service;
    private readonly ITransferService     _transfer;
    private readonly IMemberImportService _import;
    private readonly ICurrentUserService  _currentUser;
    private readonly IQrPdfExportService  _qrPdf;
    private readonly IPhotoService        _photo;
    private readonly IUnitOfWork          _uow;

    public MembersController(
        IMemberService service,
        ITransferService transfer,
        IMemberImportService import,
        ICurrentUserService currentUser,
        IQrPdfExportService qrPdf,
        IPhotoService photo,
        IUnitOfWork uow)
    {
        _service     = service;
        _transfer    = transfer;
        _import      = import;
        _currentUser = currentUser;
        _qrPdf       = qrPdf;
        _photo       = photo;
        _uow         = uow;
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

    /// <summary>
    /// Fast autocomplete search — returns only Id, FullName, TroopName.
    /// Uses a single projected SQL query: no MemberPoints/Excuses joins, no COUNT.
    /// Supports first/last name (Contains) and CustomId prefix via integer division.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<IEnumerable<MemberSearchDto>>>> Search(
        [FromQuery] string? q       = null,
        [FromQuery] Guid?   groupId = null,
        [FromQuery] int     limit   = 15)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(ApiResponse<IEnumerable<MemberSearchDto>>.Ok([]));

        q = q.Trim();

        // Parse numeric input to also match CustomId (e.g. "1000" → member 100001)
        bool isNumeric = int.TryParse(q, out int customIdVal);

        // Integer-division prefix match — 100% reliable EF Core → SQL translation.
        // CustomIds are 6 digits (100001…).  For query "1000" (len 4):
        //   divisor = 10^(6-4) = 100  →  100001 / 100 = 1000  ✓
        int divisor = (isNumeric && q.Length <= 6)
            ? (int)Math.Pow(10, 6 - q.Length)
            : 1;

        var qLower = q.ToLower();
        var results = await _uow.Members.Query()
            .Where(m => !groupId.HasValue || m.GroupId == groupId.Value)
            .Where(m =>
                // Name search — case-insensitive, partial match on first, last, or full name
                m.FirstName.ToLower().Contains(qLower) ||
                m.LastName.ToLower().Contains(qLower)  ||
                (m.FirstName + " " + m.LastName).ToLower().Contains(qLower) ||
                // CustomId prefix via integer division (pure arithmetic, no ToString)
                (isNumeric && m.CustomId / divisor == customIdVal))
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .Take(Math.Min(limit, 20))
            .Select(m => new MemberSearchDto
            {
                Id        = m.Id,
                FullName  = m.FirstName + " " + m.LastName,
                TroopName = m.Troop != null ? m.Troop.Name : ""
            })
            .ToListAsync();

        return Ok(ApiResponse<IEnumerable<MemberSearchDto>>.Ok(results));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<MemberDto>>> GetById(Guid id)
    {
        var result = await _service.GetByIdAsync(id);
        return result is null ? NotFound(ApiResponse.Fail("Member not found")) : Ok(ApiResponse<MemberDto>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse<MemberDto>>> Create([FromBody] CreateMemberDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<MemberDto>.Ok(result, "Member created"));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse<MemberDto>>> Update(Guid id, [FromBody] UpdateMemberDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return result is null ? NotFound(ApiResponse.Fail("Member not found")) : Ok(ApiResponse<MemberDto>.Ok(result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
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
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse<TransferDto>>> Transfer([FromBody] CreateTransferDto dto)
    {
        var result = await _transfer.CreateTransferAsync(dto);
        return Ok(ApiResponse<TransferDto>.Ok(result, "Member transferred successfully"));
    }

    // ── Bulk Transfer Troop ───────────────────────────────────────────────────

    /// <summary>Moves a batch of members to a new troop in one transaction.</summary>
    [HttpPost("bulk-transfer-troop")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse<BulkTransferResultDto>>> BulkTransferTroop(
        [FromBody] BulkTransferTroopDto dto)
    {
        try
        {
            var result = await _service.BulkTransferTroopAsync(dto);
            return Ok(ApiResponse<BulkTransferResultDto>.Ok(
                result, $"{result.Count} member(s) moved to {result.TroopName}"));
        }
        catch (InvalidOperationException ex)  { return NotFound(ApiResponse.Fail(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse.Fail(ex.Message)); }
    }

    // ── Auto Promote Grades ───────────────────────────────────────────────────

    /// <summary>Promotes every member to the next academic grade in one transaction.</summary>
    [HttpPost("auto-promote-grades")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse<AutoPromoteGradesResultDto>>> AutoPromoteGrades(
        [FromBody] AutoPromoteGradesDto dto)
    {
        var result = await _service.AutoPromoteGradesAsync(dto.GroupId);
        return Ok(ApiResponse<AutoPromoteGradesResultDto>.Ok(
            result, $"{result.TotalPromoted} members promoted successfully"));
    }

    // ── Grade Distribution ────────────────────────────────────────────────────

    /// <summary>Returns count of members per academic grade, in canonical order.</summary>
    [HttpGet("grade-distribution")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse<IEnumerable<GradeCountDto>>>> GetGradeDistribution()
    {
        var result = await _service.GetGradeDistributionAsync();
        return Ok(ApiResponse<IEnumerable<GradeCountDto>>.Ok(result));
    }

    /// <summary>Bulk update talaea / academic year / grade for all members in a troop at year start.</summary>
    [HttpPost("bulk-year-update")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse<int>>> BulkYearUpdate([FromBody] BulkYearUpdateDto dto)
    {
        var count = await _service.BulkYearUpdateAsync(dto);
        return Ok(ApiResponse<int>.Ok(count, $"Updated {count} member(s)"));
    }

    /// <summary>Downloads a pre-formatted .xlsx template for bulk member import.</summary>
    [HttpGet("import-template")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
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
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
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
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
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

        ImportMembersResultDto result;
        try
        {
            using var stream = file.OpenReadStream();
            result = await _import.ImportAsync(stream, effectiveTroopId.Value);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("Troop not found", StringComparison.OrdinalIgnoreCase))
        {
            // The selected troop was deleted between the page load and the import click.
            return BadRequest(ApiResponse.Fail(
                "The selected troop no longer exists. Please refresh the page, choose a valid troop from the Filters panel, and try again."));
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("Failed to generate", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(500, ApiResponse.Fail(
                "Could not generate unique IDs for all members. Please try again or contact support."));
        }
        catch (Exception ex)
        {
            // ClosedXML parse errors, XML errors, DbUpdateException, and any other
            // unexpected failures.  Walk the inner exception chain so we surface the
            // actual DB / format error rather than the generic EF Core wrapper message.
            var rootMessage = ex.Message;
            for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
                rootMessage = inner.Message;   // innermost = most specific (e.g. Npgsql error)

            return BadRequest(ApiResponse.Fail(
                $"Import failed. Make sure you are uploading the correct .xlsx template without modifying the sheet structure. Detail: {rootMessage}"));
        }

        var message = result.ImportedCount == 0
            ? $"No members imported. {result.SkippedCount} row(s) skipped."
            : $"{result.ImportedCount} member(s) imported successfully. {result.SkippedCount} row(s) skipped.";

        return Ok(ApiResponse<ImportMembersResultDto>.Ok(result, message));
    }

    /// <summary>
    /// Uploads a profile photo for a member.
    /// Accepts JPEG or PNG, max 2 MB.
    /// Returns the public image URL stored on the member record.
    /// </summary>
    [HttpPost("{id:guid}/upload-photo")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse<object>>> UploadPhoto(Guid id, IFormFile photo)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest(ApiResponse.Fail("No file uploaded"));

        if (photo.Length > 2 * 1024 * 1024)
            return BadRequest(ApiResponse.Fail("Photo exceeds 2 MB limit"));

        var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png"))
            return BadRequest(ApiResponse.Fail("Only JPG and PNG images are accepted"));

        // Load member
        var member = await _uow.Members.GetByIdAsync(id);
        if (member is null || member.IsDeleted)
            return NotFound(ApiResponse.Fail("Member not found"));

        // Delete old photo (best-effort)
        if (!string.IsNullOrWhiteSpace(member.ProfileImageUrl))
        {
            try { await _photo.DeleteAsync(member.ProfileImageUrl); } catch { /* ignore */ }
        }

        // Upload new photo
        string imageUrl;
        try
        {
            await using var stream = photo.OpenReadStream();
            imageUrl = await _photo.UploadAsync(stream, photo.FileName, id.ToString());
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse.Fail($"Photo upload failed: {ex.Message}"));
        }

        // Persist URL on the member
        member.ProfileImageUrl = imageUrl;
        member.UpdatedAt       = DateTime.UtcNow;
        _uow.Members.Update(member);
        await _uow.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { imageUrl }, "Photo uploaded successfully"));
    }

    /// <summary>Removes the profile photo for a member.</summary>
    [HttpDelete("{id:guid}/photo")]
    [Authorize(Roles = "SystemAdmin,GroupLeader,GroupLeaderAdmin")]
    public async Task<ActionResult<ApiResponse>> DeletePhoto(Guid id)
    {
        var member = await _uow.Members.GetByIdAsync(id);
        if (member is null || member.IsDeleted)
            return NotFound(ApiResponse.Fail("Member not found"));

        if (!string.IsNullOrWhiteSpace(member.ProfileImageUrl))
        {
            try { await _photo.DeleteAsync(member.ProfileImageUrl); } catch { /* ignore */ }
        }

        member.ProfileImageUrl = null;
        member.UpdatedAt       = DateTime.UtcNow;
        _uow.Members.Update(member);
        await _uow.SaveChangesAsync();

        return Ok(ApiResponse.Ok("Photo removed"));
    }
}
