using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.Services;
using ScoutsAttendance.Application.Interfaces;

namespace ScoutsAttendance.Infrastructure.Services;

/// <summary>Generates Excel (.xlsx) files using ClosedXML for all major data exports.</summary>
public class ExcelExportService : IExcelExportService
{
    private readonly IUnitOfWork _uow;

    public ExcelExportService(IUnitOfWork uow) => _uow = uow;

    // ─── Shared helpers ────────────────────────────────────────────────────────

    private static void StyleHeader(IXLRow row, int colCount)
    {
        var range = row.Worksheet.Range(row.RowNumber(), 1, row.RowNumber(), colCount);
        range.Style.Fill.BackgroundColor    = XLColor.FromHtml("#1a237e");
        range.Style.Font.FontColor          = XLColor.White;
        range.Style.Font.Bold               = true;
        range.Style.Alignment.Horizontal    = XLAlignmentHorizontalValues.Center;
    }

    private static void AddLogoRow(IXLWorksheet ws, string title)
    {
        ws.Row(1).Height = 30;
        var titleCell = ws.Cell(1, 1);
        titleCell.Value = $"🏕 {title}";
        titleCell.Style.Font.Bold     = true;
        titleCell.Style.Font.FontSize = 16;
        titleCell.Style.Font.FontColor = XLColor.FromHtml("#1a237e");
        titleCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        titleCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#e8eaf6");
    }

    private static byte[] WorkbookToBytes(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ─── Members ───────────────────────────────────────────────────────────────

    public async Task<byte[]> ExportMembersAsync(Guid? troopId = null)
    {
        var query = _uow.Members.Query()
            .IgnoreQueryFilters()
            .Include(m => m.Troop)
            .Where(m => !m.IsDeleted);

        if (troopId.HasValue) query = query.Where(m => m.TroopId == troopId.Value);

        var members = await query.OrderBy(m => m.Troop == null ? "" : m.Troop.Name).ThenBy(m => m.LastName).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Members");

        AddLogoRow(ws, "Members List — Scouts Attendance System");
        ws.Range(1, 1, 1, 12).Merge();

        var headers = new[] { "Full Name", "Troop", "Region", "Phone", "Father Phone", "Mother Phone",
                               "Date of Birth", "Academic Year", "Has Neckerchief", "Year Joined", "Address", "Joined" };
        var hRow = ws.Row(3);
        for (int i = 0; i < headers.Length; i++) hRow.Cell(i + 1).Value = headers[i];
        StyleHeader(hRow, headers.Length);

        int row = 4;
        foreach (var m in members)
        {
            ws.Cell(row, 1).Value  = m.FullName;
            ws.Cell(row, 2).Value  = m.Troop?.Name ?? "";
            ws.Cell(row, 3).Value  = m.Region ?? "";
            ws.Cell(row, 4).Value  = m.PhoneNumber ?? "";
            ws.Cell(row, 5).Value  = m.FatherPhone ?? "";
            ws.Cell(row, 6).Value  = m.MotherPhone ?? "";
            ws.Cell(row, 7).Value  = m.DateOfBirth.ToString("yyyy-MM-dd");
            ws.Cell(row, 8).Value  = m.AcademicYear ?? "";
            ws.Cell(row, 9).Value  = m.HasNeckerchief ? "Yes" : "No";
            ws.Cell(row, 10).Value = m.YearJoined?.ToString() ?? "";
            ws.Cell(row, 11).Value = m.Address ?? "";
            ws.Cell(row, 12).Value = m.CreatedAt.ToString("yyyy-MM-dd");

            if (row % 2 == 0)
                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f4f9");
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(3);

        return WorkbookToBytes(wb);
    }

    // ─── Attendance ────────────────────────────────────────────────────────────

    public async Task<byte[]> ExportAttendanceAsync(Guid? eventId, Guid? troopId, DateTime? from, DateTime? to)
    {
        var query = _uow.AttendanceRecords.Query()
            .Include(a => a.Event)
            .Include(a => a.Member).ThenInclude(m => m.Troop)
            .Where(a => !a.IsDeleted);

        if (eventId.HasValue) query = query.Where(a => a.EventId  == eventId.Value);
        if (troopId.HasValue) query = query.Where(a => a.Member.TroopId == troopId.Value);
        if (from.HasValue)    query = query.Where(a => a.Event.EventDate >= from.Value);
        if (to.HasValue)      query = query.Where(a => a.Event.EventDate <= to.Value);

        var records = await query.OrderBy(a => a.Event.EventDate).ThenBy(a => a.Member.LastName).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Attendance");

        AddLogoRow(ws, "Attendance Report — Scouts Attendance System");
        ws.Range(1, 1, 1, 8).Merge();

        var headers = new[] { "Event", "Event Date", "Member", "Troop", "Status", "Notes", "Points Awarded", "Marked At" };
        var hRow = ws.Row(3);
        for (int i = 0; i < headers.Length; i++) hRow.Cell(i + 1).Value = headers[i];
        StyleHeader(hRow, headers.Length);

        int row = 4;
        foreach (var r in records)
        {
            ws.Cell(row, 1).Value = r.Event?.Name ?? "";
            ws.Cell(row, 2).Value = r.Event?.EventDate.ToString("yyyy-MM-dd") ?? "";
            ws.Cell(row, 3).Value = r.Member?.FullName ?? "";
            ws.Cell(row, 4).Value = r.Member?.Troop?.Name ?? "";
            ws.Cell(row, 5).Value = r.Status.ToString();
            ws.Cell(row, 6).Value = r.Notes ?? "";
            ws.Cell(row, 7).Value = r.AutoPoints?.Points ?? 0;
            ws.Cell(row, 8).Value = r.MarkedAt.ToString("yyyy-MM-dd HH:mm");

            // Colour-code status
            var statusColor = r.Status.ToString() switch
            {
                "Present" => XLColor.FromHtml("#c8e6c9"),
                "Late"    => XLColor.FromHtml("#fff9c4"),
                "Absent"  => XLColor.FromHtml("#ffcdd2"),
                "Excused" => XLColor.FromHtml("#bbdefb"),
                _         => XLColor.NoColor
            };
            ws.Cell(row, 5).Style.Fill.BackgroundColor = statusColor;

            if (row % 2 == 0)
                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f4f9");
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(3);

        return WorkbookToBytes(wb);
    }

    // ─── Member Points ─────────────────────────────────────────────────────────

    public async Task<byte[]> ExportPointsAsync(Guid? troopId = null)
    {
        var query = _uow.MemberPoints.Query()
            .Include(p => p.Member).ThenInclude(m => m.Troop)
            .Include(p => p.Category)
            .Where(p => !p.IsDeleted);

        if (troopId.HasValue) query = query.Where(p => p.Member.TroopId == troopId.Value);

        var pts = await query.OrderBy(p => p.Member.LastName).ThenByDescending(p => p.Date).ToListAsync();

        using var wb = new XLWorkbook();

        // Summary sheet
        var wsSummary = wb.Worksheets.Add("Summary");
        AddLogoRow(wsSummary, "Member Points Summary");
        wsSummary.Range(1, 1, 1, 5).Merge();

        var summaryHeaders = new[] { "Member", "Troop", "Total Points" };
        var shRow = wsSummary.Row(3);
        for (int i = 0; i < summaryHeaders.Length; i++) shRow.Cell(i + 1).Value = summaryHeaders[i];
        StyleHeader(shRow, summaryHeaders.Length);

        var grouped = pts.GroupBy(p => p.Member)
                         .OrderByDescending(g => g.Sum(p => p.Points))
                         .ToList();
        int sRow = 4;
        foreach (var g in grouped)
        {
            wsSummary.Cell(sRow, 1).Value = g.Key?.FullName ?? "";
            wsSummary.Cell(sRow, 2).Value = g.Key?.Troop?.Name ?? "";
            wsSummary.Cell(sRow, 3).Value = g.Sum(p => p.Points);
            sRow++;
        }
        wsSummary.Columns().AdjustToContents();

        // Detail sheet
        var wsDetail = wb.Worksheets.Add("Detail");
        AddLogoRow(wsDetail, "Member Points Detail");
        wsDetail.Range(1, 1, 1, 7).Merge();

        var detailHeaders = new[] { "Member", "Troop", "Category", "Points", "Date", "Note", "Type" };
        var dHRow = wsDetail.Row(3);
        for (int i = 0; i < detailHeaders.Length; i++) dHRow.Cell(i + 1).Value = detailHeaders[i];
        StyleHeader(dHRow, detailHeaders.Length);

        int dRow = 4;
        foreach (var p in pts)
        {
            wsDetail.Cell(dRow, 1).Value = p.Member?.FullName ?? "";
            wsDetail.Cell(dRow, 2).Value = p.Member?.Troop?.Name ?? "";
            wsDetail.Cell(dRow, 3).Value = p.Category?.Name ?? "";
            wsDetail.Cell(dRow, 4).Value = p.Points;
            wsDetail.Cell(dRow, 5).Value = p.Date.ToString("yyyy-MM-dd");
            wsDetail.Cell(dRow, 6).Value = p.Note ?? "";
            wsDetail.Cell(dRow, 7).Value = p.AttendanceRecordId.HasValue ? "Auto" : "Manual";
            if (dRow % 2 == 0)
                wsDetail.Range(dRow, 1, dRow, detailHeaders.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f4f9");
            dRow++;
        }
        wsDetail.Columns().AdjustToContents();
        wsDetail.SheetView.FreezeRows(3);

        return WorkbookToBytes(wb);
    }

    // ─── Troop Points ──────────────────────────────────────────────────────────

    public async Task<byte[]> ExportTroopPointsAsync(Guid? troopId = null)
    {
        var query = _uow.TroopPoints.Query()
            .Include(p => p.Troop)
            .Include(p => p.Category)
            .Where(p => !p.IsDeleted);

        if (troopId.HasValue) query = query.Where(p => p.TroopId == troopId.Value);

        var pts = await query.OrderBy(p => p.Troop.Name).ThenByDescending(p => p.Date).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Troop Points");

        AddLogoRow(ws, "Troop Points Report");
        ws.Range(1, 1, 1, 5).Merge();

        var headers = new[] { "Troop", "Category", "Points", "Date", "Note" };
        var hRow = ws.Row(3);
        for (int i = 0; i < headers.Length; i++) hRow.Cell(i + 1).Value = headers[i];
        StyleHeader(hRow, headers.Length);

        int row = 4;
        foreach (var p in pts)
        {
            ws.Cell(row, 1).Value = p.Troop?.Name ?? "";
            ws.Cell(row, 2).Value = p.Category?.Name ?? "";
            ws.Cell(row, 3).Value = p.Points;
            ws.Cell(row, 4).Value = p.Date.ToString("yyyy-MM-dd");
            ws.Cell(row, 5).Value = p.Note ?? "";
            if (row % 2 == 0)
                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f4f9");
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(3);
        return WorkbookToBytes(wb);
    }

    // ─── Exam Scores ───────────────────────────────────────────────────────────

    public async Task<byte[]> ExportExamScoresAsync(Guid? troopId = null, int? year = null)
    {
        var query = _uow.MemberExamScores.Query()
            .Include(x => x.Member).ThenInclude(m => m.Troop)
            .Where(x => !x.IsDeleted);

        if (troopId.HasValue) query = query.Where(x => x.Member.TroopId == troopId.Value);
        if (year.HasValue)    query = query.Where(x => x.Year == year.Value);

        var scores = await query.OrderBy(x => x.Member.Troop.Name)
                                .ThenBy(x => x.Member.LastName)
                                .ThenBy(x => x.Year)
                                .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Exam Scores");

        AddLogoRow(ws, "End-of-Year Exam Scores");
        ws.Range(1, 1, 1, 6).Merge();

        var headers = new[] { "Member", "Troop", "Year", "Score (/100)", "Grade", "Notes" };
        var hRow = ws.Row(3);
        for (int i = 0; i < headers.Length; i++) hRow.Cell(i + 1).Value = headers[i];
        StyleHeader(hRow, headers.Length);

        int row = 4;
        foreach (var s in scores)
        {
            var grade = s.Score switch
            {
                >= 90 => "Excellent",
                >= 75 => "Very Good",
                >= 60 => "Good",
                >= 50 => "Pass",
                _     => "Fail"
            };
            ws.Cell(row, 1).Value = s.Member?.FullName ?? "";
            ws.Cell(row, 2).Value = s.Member?.Troop?.Name ?? "";
            ws.Cell(row, 3).Value = s.Year;
            ws.Cell(row, 4).Value = s.Score;
            ws.Cell(row, 5).Value = grade;
            ws.Cell(row, 6).Value = s.Notes ?? "";

            // Colour pass/fail
            var scoreColor = s.Score >= 50 ? XLColor.FromHtml("#c8e6c9") : XLColor.FromHtml("#ffcdd2");
            ws.Cell(row, 4).Style.Fill.BackgroundColor = scoreColor;

            if (row % 2 == 0)
                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f4f9");
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(3);
        return WorkbookToBytes(wb);
    }
}
