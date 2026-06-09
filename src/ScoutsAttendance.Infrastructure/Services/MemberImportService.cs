using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScoutsAttendance.Application.DTOs.Members;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;
using ScoutsAttendance.Infrastructure.Data;

namespace ScoutsAttendance.Infrastructure.Services;

public class MemberImportService : IMemberImportService
{
    private readonly ApplicationDbContext _db;
    private readonly IQrCodeService       _qrCode;
    private readonly ICustomIdService     _customId;
    private readonly ICurrentUserService  _currentUser;
    private readonly ILogger<MemberImportService> _logger;

    // Column indices (1-based, matching the template)
    private const int ColFirstName   = 1;
    private const int ColLastName    = 2;
    private const int ColGender      = 3;
    private const int ColPhone       = 4;
    private const int ColFatherPhone = 5;
    private const int ColMotherPhone = 6;
    private const int ColAddress     = 7;
    private const int ColRegion      = 8;
    private const int ColGrade       = 9;
    private const int ColYearJoined  = 10;
    private const int ColFoulard     = 11;
    private const int ColNotes       = 12;
    private const int TotalCols      = 12;

    public MemberImportService(
        ApplicationDbContext db,
        IQrCodeService qrCode,
        ICustomIdService customId,
        ICurrentUserService currentUser,
        ILogger<MemberImportService> logger)
    {
        _db          = db;
        _qrCode      = qrCode;
        _customId    = customId;
        _currentUser = currentUser;
        _logger      = logger;
    }

    // ─── Template ─────────────────────────────────────────────────────────────

    public byte[] GenerateTemplate()
    {
        using var wb = new XLWorkbook();

        // ── Data sheet ────────────────────────────────────────────────────────
        var ws = wb.Worksheets.Add("Members");

        // Title row
        ws.Row(1).Height = 28;
        var title = ws.Cell(1, 1);
        title.Value = "🏕  Scout Members — Bulk Import Template";
        title.Style.Font.Bold      = true;
        title.Style.Font.FontSize  = 14;
        title.Style.Font.FontColor = XLColor.FromHtml("#1a237e");
        title.Style.Fill.BackgroundColor = XLColor.FromHtml("#e8eaf6");
        ws.Range(1, 1, 1, TotalCols).Merge();

        // Header row (row 2)
        string[] headers =
        [
            "First Name *", "Last Name *", "Gender * (Male/Female)",
            "Phone", "Father Phone", "Mother Phone",
            "Address", "Region", "Academic Grade",
            "Year Joined Scouts", "Has Foulard (Yes/No)", "Notes"
        ];

        var hRow = ws.Row(2);
        hRow.Height = 22;
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = hRow.Cell(i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold               = true;
            cell.Style.Font.FontColor          = XLColor.White;
            cell.Style.Fill.BackgroundColor    = XLColor.FromHtml("#1a237e");
            cell.Style.Alignment.Horizontal    = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.WrapText      = true;
            cell.Style.Border.BottomBorder     = XLBorderStyleValues.Thin;

            // Mark required columns
            if (i < 3)
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#283593");
        }

        // Sample row (row 3)
        ws.Cell(3, ColFirstName).Value   = "Ahmed";
        ws.Cell(3, ColLastName).Value    = "Hassan";
        ws.Cell(3, ColGender).Value      = "Male";
        ws.Cell(3, ColPhone).Value       = "01012345678";
        ws.Cell(3, ColFatherPhone).Value = "01098765432";
        ws.Cell(3, ColMotherPhone).Value = "01112345678";
        ws.Cell(3, ColAddress).Value     = "123 Nile St, Cairo";
        ws.Cell(3, ColRegion).Value      = "North";
        ws.Cell(3, ColGrade).Value       = "Grade 5";
        ws.Cell(3, ColYearJoined).Value  = 2022;
        ws.Cell(3, ColFoulard).Value     = "Yes";
        ws.Cell(3, ColNotes).Value       = "Example row — overwrite or delete this row";

        // Style sample row
        ws.Range(3, 1, 3, TotalCols).Style.Fill.BackgroundColor = XLColor.FromHtml("#fff8e1");
        ws.Range(3, 1, 3, TotalCols).Style.Font.Italic = true;
        ws.Range(3, 1, 3, TotalCols).Style.Font.FontColor = XLColor.FromHtml("#5d4037");

        // ── Hidden "Lists" sheet — holds dropdown sources (grade list too long for inline) ──
        var wsLists = wb.Worksheets.Add("Lists");
        wsLists.Visibility = XLWorksheetVisibility.Hidden;

        // Gender list (col A)
        wsLists.Cell(1, 1).Value = "Male";
        wsLists.Cell(2, 1).Value = "Female";

        // Foulard list (col B)
        wsLists.Cell(1, 2).Value = "Yes";
        wsLists.Cell(2, 2).Value = "No";

        // Academic Grade list (col C)
        string[] grades =
        [
            "3 ابتدائي", "4 ابتدائي", "5 ابتدائي", "6 ابتدائي",
            "1 اعدادي",  "2 اعدادي",  "3 اعدادي",
            "1 ثانوي",   "2 ثانوي",   "3 ثانوي",
            "1 جامعة",   "2 جامعة",   "3 جامعة",
            "4 جامعة",   "5 جامعة",   "6 جامعة",
            "خريج"
        ];
        for (int i = 0; i < grades.Length; i++)
            wsLists.Cell(i + 1, 3).Value = grades[i];

        // Data validation — apply from row 3 (sample row) to 2000
        // Gender (col A of Lists)
        var genderValidation = ws.Range(3, ColGender, 2000, ColGender).CreateDataValidation();
        genderValidation.List(wsLists.Range(1, 1, 2, 1), true);

        // Foulard (col B of Lists)
        var foulardValidation = ws.Range(3, ColFoulard, 2000, ColFoulard).CreateDataValidation();
        foulardValidation.List(wsLists.Range(1, 2, 2, 2), true);

        // Academic Grade (col C of Lists)
        var gradeValidation = ws.Range(3, ColGrade, 2000, ColGrade).CreateDataValidation();
        gradeValidation.List(wsLists.Range(1, 3, grades.Length, 3), true);

        // Column widths
        ws.Column(ColFirstName).Width   = 18;
        ws.Column(ColLastName).Width    = 18;
        ws.Column(ColGender).Width      = 22;
        ws.Column(ColPhone).Width       = 16;
        ws.Column(ColFatherPhone).Width = 16;
        ws.Column(ColMotherPhone).Width = 16;
        ws.Column(ColAddress).Width     = 28;
        ws.Column(ColRegion).Width      = 16;
        ws.Column(ColGrade).Width       = 18;
        ws.Column(ColYearJoined).Width  = 20;
        ws.Column(ColFoulard).Width     = 20;
        ws.Column(ColNotes).Width       = 30;

        ws.SheetView.FreezeRows(2);

        // ── Instructions sheet ────────────────────────────────────────────────
        var wsInfo = wb.Worksheets.Add("Instructions");
        wsInfo.Row(1).Height = 28;
        wsInfo.Cell(1, 1).Value = "📋  Import Instructions";
        wsInfo.Cell(1, 1).Style.Font.Bold     = true;
        wsInfo.Cell(1, 1).Style.Font.FontSize = 14;
        wsInfo.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#1a237e");
        wsInfo.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#e8eaf6");
        wsInfo.Range(1, 1, 1, 3).Merge();

        string[][] instructions =
        [
            ["Column", "Required", "Description / Accepted values"],
            ["First Name",   "Yes", "Member's first name"],
            ["Last Name",    "Yes", "Member's last name"],
            ["Gender",       "Yes", "Must be exactly: Male  OR  Female"],
            ["Phone",        "No",  "Member's personal phone number"],
            ["Father Phone", "No",  "Father's phone number"],
            ["Mother Phone", "No",  "Mother's phone number"],
            ["Address",      "No",  "Member's home address"],
            ["Region",       "No",  "Region / area name (e.g. North, South)"],
            ["Academic Grade","No", "Select from dropdown: 3 ابتدائي … خريج"],
            ["Year Joined",  "No",  "4-digit year, e.g. 2022"],
            ["Has Foulard",  "No",  "Yes  OR  No  (default: No)"],
            ["Notes",        "No",  "Any free-text notes"],
        ];

        for (int r = 0; r < instructions.Length; r++)
        {
            for (int c = 0; c < instructions[r].Length; c++)
                wsInfo.Cell(r + 2, c + 1).Value = instructions[r][c];

            if (r == 0)
            {
                wsInfo.Row(r + 2).Style.Font.Bold = true;
                wsInfo.Row(r + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#1a237e");
                wsInfo.Row(r + 2).Style.Font.FontColor = XLColor.White;
            }
            else if (r % 2 == 0)
            {
                wsInfo.Row(r + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f4f9");
            }
        }

        wsInfo.Row(5).Style.Fill.BackgroundColor = XLColor.FromHtml("#fff9c4"); // highlight required gender row
        wsInfo.Columns().AdjustToContents();

        string[] notes =
        [
            "",
            "NOTES:",
            "• Rows 1–2 in the Members sheet are headers — do NOT delete them.",
            "• Row 3 is a sample row — you can delete it or overwrite it with real data.",
            "• Start entering member data from Row 3 onwards (overwrite the sample row).",
            "• The system automatically generates a unique 6-digit ID for each member.",
            "• Male members receive an ODD last digit (e.g. 100001), Female members an EVEN last digit (e.g. 100002).",
            "• Duplicate rows (same First Name + Last Name + Phone) are skipped automatically.",
            "• Maximum file size: 5 MB.",
        ];

        int noteRow = instructions.Length + 4;
        foreach (var note in notes)
        {
            wsInfo.Cell(noteRow, 1).Value = note;
            if (note.StartsWith("NOTES:"))
                wsInfo.Cell(noteRow, 1).Style.Font.Bold = true;
            noteRow++;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ─── Import ───────────────────────────────────────────────────────────────

    public async Task<ImportMembersResultDto> ImportAsync(Stream fileStream, Guid troopId)
    {
        var result = new ImportMembersResultDto();

        _logger.LogInformation("Member import started by user {UserId} for troop {TroopId} at {Time}",
            _currentUser.UserId, troopId, DateTime.UtcNow);

        var troop = await _db.Troops.FirstOrDefaultAsync(t => t.Id == troopId && !t.IsDeleted)
            ?? throw new InvalidOperationException("Troop not found");

        // ClosedXML throws various exceptions (XmlException, InvalidOperationException,
        // ClosedXMLException) if the file is corrupted, password-protected, or saved
        // with Excel features that ClosedXML does not support (e.g. custom data
        // validation formulas added by newer Excel versions).  Wrap in a friendlier
        // message so the controller can return a clear 400 instead of a raw 500.
        using var wb = OpenWorkbookSafe(fileStream);
        var ws = wb.Worksheets.First();

        // Build a set of existing (firstName+lastName+phone) keys to detect duplicates
        var existingKeys = (await _db.Members
            .Where(m => !m.IsDeleted)
            .Select(m => (m.FirstName.ToLower() + "|" + m.LastName.ToLower() + "|" + (m.PhoneNumber ?? "")).Trim())
            .ToListAsync())
            .ToHashSet();

        // Track duplicates within this import batch too
        var batchKeys = new HashSet<string>();

        // Track CustomIds allocated in this batch (not yet in DB) so GenerateAsync won't duplicate them
        var allocatedCustomIds = new HashSet<int>();

        var toInsert = new List<Member>();

        // Row 1 = title, Row 2 = headers, Row 3+ = data (sample row may or may not be there)
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 2;

        for (int rowNum = 3; rowNum <= lastRow; rowNum++)
        {
            var row = ws.Row(rowNum);

            // Skip completely empty rows
            if (ws.Range(rowNum, 1, rowNum, TotalCols).IsEmpty()) continue;

            string firstName = row.Cell(ColFirstName).GetString().Trim();
            string lastName  = row.Cell(ColLastName).GetString().Trim();
            string genderStr = row.Cell(ColGender).GetString().Trim();

            // Auto-skip the built-in sample row if the user forgot to delete it
            var notesPreview = row.Cell(ColNotes).GetString();
            if (notesPreview.Contains("Example row", StringComparison.OrdinalIgnoreCase) &&
                firstName.Equals("Ahmed", StringComparison.OrdinalIgnoreCase))
                continue;

            // ── Validation ────────────────────────────────────────────────────
            if (string.IsNullOrEmpty(firstName))
            {
                result.SkippedRows.Add(new SkippedRowDto
                    { RowNumber = rowNum, LastName = lastName, Reason = "Missing required field: First Name" });
                continue;
            }
            if (string.IsNullOrEmpty(lastName))
            {
                result.SkippedRows.Add(new SkippedRowDto
                    { RowNumber = rowNum, FirstName = firstName, Reason = "Missing required field: Last Name" });
                continue;
            }

            Gender gender;
            if (genderStr.Equals("Male", StringComparison.OrdinalIgnoreCase))
                gender = Gender.Male;
            else if (genderStr.Equals("Female", StringComparison.OrdinalIgnoreCase))
                gender = Gender.Female;
            else
            {
                result.SkippedRows.Add(new SkippedRowDto
                {
                    RowNumber = rowNum, FirstName = firstName, LastName = lastName,
                    Reason = $"Invalid Gender value \"{genderStr}\" — must be Male or Female"
                });
                continue;
            }

            // ── Phone ─────────────────────────────────────────────────────────
            string? phone = NullIfEmpty(row.Cell(ColPhone).GetString());

            // ── Duplicate check ───────────────────────────────────────────────
            string key = $"{firstName.ToLower()}|{lastName.ToLower()}|{phone ?? ""}";
            if (existingKeys.Contains(key) || batchKeys.Contains(key))
            {
                result.SkippedRows.Add(new SkippedRowDto
                {
                    RowNumber = rowNum, FirstName = firstName, LastName = lastName,
                    Reason = "Duplicate — member with same name and phone already exists"
                });
                continue;
            }
            batchKeys.Add(key);

            // ── Optional fields ───────────────────────────────────────────────
            string? fatherPhone  = NullIfEmpty(row.Cell(ColFatherPhone).GetString());
            string? motherPhone  = NullIfEmpty(row.Cell(ColMotherPhone).GetString());
            string? address      = NullIfEmpty(row.Cell(ColAddress).GetString());
            string? region       = NullIfEmpty(row.Cell(ColRegion).GetString());
            string? grade        = NullIfEmpty(row.Cell(ColGrade).GetString());
            string? foulardStr   = NullIfEmpty(row.Cell(ColFoulard).GetString());
            string? notes        = NullIfEmpty(row.Cell(ColNotes).GetString());

            int? yearJoined = null;
            var yearCell = row.Cell(ColYearJoined);
            if (!yearCell.IsEmpty())
            {
                if (yearCell.TryGetValue(out double yDbl))
                    yearJoined = (int)yDbl;
                else if (int.TryParse(yearCell.GetString(), out int yInt))
                    yearJoined = yInt;
            }

            bool hasFoulard = foulardStr?.Equals("yes", StringComparison.OrdinalIgnoreCase) ?? false;

            // ── Build entity ──────────────────────────────────────────────────
            // Pass allocatedCustomIds so the service skips IDs already reserved
            // in this batch but not yet persisted to the database.
            var customId = await _customId.GenerateAsync(gender, allocatedCustomIds);
            allocatedCustomIds.Add(customId);

            var member = new Member
            {
                FirstName      = firstName,
                LastName       = lastName,
                Gender         = gender,
                CustomId       = customId,
                PhoneNumber    = phone,
                FatherPhone    = fatherPhone,
                MotherPhone    = motherPhone,
                Address        = address,
                Region         = region,
                AcademicYear   = grade,
                YearJoined     = yearJoined,
                HasNeckerchief = hasFoulard,
                Notes          = notes,
                TroopId        = troop.Id,
                GroupId        = troop.GroupId,
                DateOfBirth    = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)    // default — no DOB column in template
            };
            member.QrCode = _qrCode.GenerateQrCodeToken(customId);

            toInsert.Add(member);
            existingKeys.Add(key);   // prevent later rows in same file from matching
        }

        // ── Single transaction insert ──────────────────────────────────────────
        if (toInsert.Count > 0)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                await _db.Members.AddRangeAsync(toInsert);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        result.ImportedCount = toInsert.Count;
        result.SkippedCount  = result.SkippedRows.Count;

        _logger.LogInformation(
            "Member import completed: {Imported} imported, {Skipped} skipped by user {UserId}",
            result.ImportedCount, result.SkippedCount, _currentUser.UserId);

        return result;
    }

    /// <summary>
    /// Opens the workbook and converts any ClosedXML / XML parse exception into a
    /// descriptive <see cref="InvalidOperationException"/> so the controller can
    /// return a friendly 400 response instead of an opaque 500.
    /// </summary>
    private static XLWorkbook OpenWorkbookSafe(Stream stream)
    {
        try
        {
            return new XLWorkbook(stream);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "The uploaded file could not be opened. " +
                "Make sure you are using the downloaded .xlsx template and have not changed " +
                "the sheet names, added passwords, or saved it with a non-standard Excel feature. " +
                $"Detail: {ex.Message}", ex);
        }
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
