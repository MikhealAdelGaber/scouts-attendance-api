using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Admin;
using ScoutsAttendance.Application.DTOs.Projects;
using ScoutsAttendance.Application.DTOs.Reports;
using ScoutsAttendance.Application.Services;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Enums;

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

    // ─── Members Full ──────────────────────────────────────────────────────────

    public async Task<byte[]> ExportMembersFullAsync(Guid? troopId = null)
    {
        var query = _uow.Members.Query()
            .IgnoreQueryFilters()
            .Include(m => m.Troop)
            .Include(m => m.MemberPoints)
            .Where(m => !m.IsDeleted);

        if (troopId.HasValue) query = query.Where(m => m.TroopId == troopId.Value);

        var members = await query
            .OrderBy(m => m.Troop == null ? "" : m.Troop.Name)
            .ThenBy(m => m.LastName)
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Members");

        const int colCount = 16;
        AddLogoRow(ws, "Members Full Report — Scouts Attendance System");
        ws.Range(1, 1, 1, colCount).Merge();

        var headers = new[]
        {
            "Custom ID", "Full Name", "Gender", "Troop", "Region", "Phone",
            "Father Phone", "Mother Phone", "Date of Birth", "Academic Year",
            "Has Neckerchief", "Year Joined", "Address", "Notes",
            "Total Points", "Joined"
        };
        var hRow = ws.Row(3);
        for (int i = 0; i < headers.Length; i++) hRow.Cell(i + 1).Value = headers[i];
        StyleHeader(hRow, headers.Length);

        int row = 4;
        foreach (var m in members)
        {
            var totalPoints = m.MemberPoints?.Sum(p => p.Points) ?? 0;
            ws.Cell(row, 1).Value  = m.CustomId;
            ws.Cell(row, 2).Value  = m.FullName;
            ws.Cell(row, 3).Value  = m.Gender == Gender.Male ? "Male" : "Female";
            ws.Cell(row, 4).Value  = m.Troop?.Name ?? "";
            ws.Cell(row, 5).Value  = m.Region ?? "";
            ws.Cell(row, 6).Value  = m.PhoneNumber ?? "";
            ws.Cell(row, 7).Value  = m.FatherPhone ?? "";
            ws.Cell(row, 8).Value  = m.MotherPhone ?? "";
            ws.Cell(row, 9).Value  = m.DateOfBirth.ToString("yyyy-MM-dd");
            ws.Cell(row, 10).Value = m.AcademicYear ?? "";
            ws.Cell(row, 11).Value = m.HasNeckerchief ? "Yes" : "No";
            ws.Cell(row, 12).Value = m.YearJoined?.ToString() ?? "";
            ws.Cell(row, 13).Value = m.Address ?? "";
            ws.Cell(row, 14).Value = m.Notes ?? "";
            ws.Cell(row, 15).Value = totalPoints;
            ws.Cell(row, 16).Value = m.CreatedAt.ToString("yyyy-MM-dd");

            if (row % 2 == 0)
                ws.Range(row, 1, row, colCount).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f4f9");
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(3);
        return WorkbookToBytes(wb);
    }

    // ─── Attendance Rate ───────────────────────────────────────────────────────

    public async Task<IEnumerable<AttendanceRateDto>> GetAttendanceRateAsync(
        Guid? troopId, DateTime? from, DateTime? to)
    {
        // ── 1. Load events in the requested date range ──────────────────────────
        var evQuery = _uow.Events.Query().Where(e => !e.IsDeleted);
        if (from.HasValue) evQuery = evQuery.Where(e => e.EventDate >= from.Value);
        if (to.HasValue)   evQuery = evQuery.Where(e => e.EventDate <= to.Value);
        var events = await evQuery.ToListAsync();
        if (!events.Any()) return [];

        // ── 2. Determine relevant group IDs ────────────────────────────────────
        var groupIds = events.Select(e => e.GroupId).Distinct().ToList();

        // ── 3. Load members in scope ────────────────────────────────────────────
        var membersQuery = _uow.Members.Query()
            .Include(m => m.Troop)
            .Include(m => m.Excuses)
            .Where(m => groupIds.Contains(m.GroupId) && !m.IsDeleted);
        if (troopId.HasValue)
            membersQuery = membersQuery.Where(m => m.TroopId == troopId.Value);
        var members = await membersQuery.ToListAsync();
        if (!members.Any()) return [];

        var memberIds = members.Select(m => m.Id).ToList();

        // ── 4. Load attendance records ──────────────────────────────────────────
        var eventIds = events.Select(e => e.Id).ToList();
        var allRecords = await _uow.AttendanceRecords.Query()
            .Where(a => eventIds.Contains(a.EventId) && !a.IsDeleted)
            .ToListAsync();

        var recordLookup = allRecords
            .GroupBy(a => a.EventId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(a => a.MemberId, a => a.Status));

        // ── 5. Load latest exam score per member (in memory to avoid LINQ translation) ──
        var allExamScores = await _uow.MemberExamScores.Query()
            .Where(e => memberIds.Contains(e.MemberId))
            .ToListAsync();
        var examMap = allExamScores
            .GroupBy(e => e.MemberId)
            .ToDictionary(g => g.Key, g => (decimal?)g.OrderByDescending(e => e.Year).First().Score);

        // ── 6. Load projects per group ──────────────────────────────────────────
        var projects = await _uow.Projects.Query()
            .Where(p => groupIds.Contains(p.GroupId) && !p.IsDeleted)
            .ToListAsync();
        var projectsPerGroup = projects.GroupBy(p => p.GroupId)
            .ToDictionary(g => g.Key, g => g.Count());

        // ── 7. Load project scores per member (score > 0 = completed) ──────────
        var projectScores = await _uow.ProjectScores.Query()
            .Where(s => memberIds.Contains(s.MemberId) && s.Score > 0)
            .GroupBy(s => s.MemberId)
            .Select(g => new { MemberId = g.Key, Count = g.Count() })
            .ToListAsync();
        var projectScoreMap = projectScores.ToDictionary(x => x.MemberId, x => x.Count);

        // ── 8. Load total points per member ─────────────────────────────────────
        var pointsList = await _uow.MemberPoints.Query()
            .Where(p => memberIds.Contains(p.MemberId))
            .GroupBy(p => p.MemberId)
            .Select(g => new { MemberId = g.Key, Total = g.Sum(p => p.Points) })
            .ToListAsync();
        var pointsMap = pointsList.ToDictionary(x => x.MemberId, x => x.Total);

        // ── 9. Compute per-member stats ─────────────────────────────────────────
        var results = new List<AttendanceRateDto>(members.Count);

        foreach (var m in members)
        {
            int present = 0, late = 0, tooLate = 0, excused = 0, absent = 0, total = 0;

            foreach (var ev in events)
            {
                if (ev.GroupId != m.GroupId) continue;
                if (ev.TroopId.HasValue && ev.TroopId != m.TroopId) continue;

                total++;

                if (recordLookup.TryGetValue(ev.Id, out var byMember) &&
                    byMember.TryGetValue(m.Id, out var status))
                {
                    switch (status)
                    {
                        case AttendanceStatus.Present: present++;  break;
                        case AttendanceStatus.Late:    late++;     break;
                        case AttendanceStatus.TooLate: tooLate++; break;
                        case AttendanceStatus.Excused: excused++; break;
                        default:                       absent++;   break;
                    }
                }
                else
                {
                    if (m.HasActiveExcuse(ev.EventDate)) excused++;
                    else                                 absent++;
                }
            }

            if (total == 0) continue;

            results.Add(new AttendanceRateDto
            {
                MemberId          = m.Id,
                MemberName        = m.FullName,
                TroopName         = m.Troop?.Name ?? "",
                AcademicGrade     = m.AcademicYear,
                TotalEvents       = total,
                Present           = present,
                Late              = late,
                TooLate           = tooLate,
                Excused           = excused,
                Absent            = absent,
                LatestExamScore   = examMap.GetValueOrDefault(m.Id),
                TotalProjects     = projectsPerGroup.GetValueOrDefault(m.GroupId),
                ProjectsCompleted = projectScoreMap.GetValueOrDefault(m.Id),
                TotalPoints       = pointsMap.GetValueOrDefault(m.Id)
            });
        }

        return results.OrderByDescending(r => r.Rate).ThenBy(r => r.MemberName);
    }

    public async Task<byte[]> ExportAttendanceRateAsync(Guid? troopId, DateTime? from, DateTime? to)
    {
        var rates = (await GetAttendanceRateAsync(troopId, from, to)).ToList();

        using var wb = new XLWorkbook();

        // ── Sheet: Full Report ────────────────────────────────────────────────
        var wsSummary = wb.Worksheets.Add("Attendance Report");
        AddLogoRow(wsSummary, "Attendance Rate Report — Scouts Attendance System");
        wsSummary.Range(1, 1, 1, 14).Merge();

        // Date range subtitle
        var rangeLabel = $"Period: {(from.HasValue ? from.Value.ToString("yyyy-MM-dd") : "All")} → {(to.HasValue ? to.Value.ToString("yyyy-MM-dd") : "All")}";
        wsSummary.Cell(2, 1).Value = rangeLabel;
        wsSummary.Cell(2, 1).Style.Font.Italic = true;
        wsSummary.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#555555");
        wsSummary.Range(2, 1, 2, 14).Merge();

        const int sumCols = 14;
        var sumHeaders = new[]
        {
            "Rank", "Member", "Troop", "Grade",
            "Total Events", "Present", "Late", "Too Late", "Excused", "Absent",
            "Attendance %", "Exam Score", "Projects %", "Total Points"
        };
        var shRow = wsSummary.Row(4);
        for (int i = 0; i < sumHeaders.Length; i++) shRow.Cell(i + 1).Value = sumHeaders[i];
        StyleHeader(shRow, sumHeaders.Length);

        int sRow = 5;
        foreach (var r in rates)
        {
            bool alt = sRow % 2 == 0;
            var rowBg = XLColor.FromHtml(alt ? "#f3f4f9" : "#ffffff");

            wsSummary.Cell(sRow,  1).Value = sRow - 4;
            wsSummary.Cell(sRow,  2).Value = r.MemberName;
            wsSummary.Cell(sRow,  3).Value = r.TroopName;
            wsSummary.Cell(sRow,  4).Value = r.AcademicGrade ?? "—";
            wsSummary.Cell(sRow,  5).Value = r.TotalEvents;
            wsSummary.Cell(sRow,  6).Value = r.Present;
            wsSummary.Cell(sRow,  7).Value = r.Late;
            wsSummary.Cell(sRow,  8).Value = r.TooLate;
            wsSummary.Cell(sRow,  9).Value = r.Excused;
            wsSummary.Cell(sRow, 10).Value = r.Absent;

            // Attendance %
            wsSummary.Cell(sRow, 11).Value = r.Rate / 100.0;
            wsSummary.Cell(sRow, 11).Style.NumberFormat.Format = "0.0%";
            wsSummary.Cell(sRow, 11).Style.Fill.BackgroundColor = r.Rate switch
            {
                >= 90 => XLColor.FromHtml("#c8e6c9"),
                >= 70 => XLColor.FromHtml("#fff9c4"),
                >= 50 => XLColor.FromHtml("#ffe0b2"),
                _     => XLColor.FromHtml("#ffcdd2")
            };
            wsSummary.Cell(sRow, 11).Style.Font.Bold = true;

            // Exam Score
            if (r.LatestExamScore.HasValue)
            {
                wsSummary.Cell(sRow, 12).Value = (double)r.LatestExamScore.Value;
                wsSummary.Cell(sRow, 12).Style.Fill.BackgroundColor = r.LatestExamScore.Value switch
                {
                    >= 80 => XLColor.FromHtml("#c8e6c9"),
                    >= 60 => XLColor.FromHtml("#fff9c4"),
                    _     => XLColor.FromHtml("#ffcdd2")
                };
            }
            else { wsSummary.Cell(sRow, 12).Value = "—"; }

            // Projects %
            if (r.ProjectRate.HasValue)
            {
                wsSummary.Cell(sRow, 13).Value = r.ProjectRate.Value / 100.0;
                wsSummary.Cell(sRow, 13).Style.NumberFormat.Format = "0.0%";
                wsSummary.Cell(sRow, 13).Style.Fill.BackgroundColor = r.ProjectRate.Value switch
                {
                    >= 80 => XLColor.FromHtml("#c8e6c9"),
                    >= 60 => XLColor.FromHtml("#fff9c4"),
                    _     => XLColor.FromHtml("#ffcdd2")
                };
            }
            else { wsSummary.Cell(sRow, 13).Value = "—"; }

            // Total Points
            wsSummary.Cell(sRow, 14).Value = (double)r.TotalPoints;
            wsSummary.Cell(sRow, 14).Style.NumberFormat.Format = "#,##0.##";
            wsSummary.Cell(sRow, 14).Style.Font.Bold = true;

            // Row background for non-colour cells
            foreach (int c in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 14 })
                wsSummary.Cell(sRow, c).Style.Fill.BackgroundColor = rowBg;

            sRow++;
        }

        // Footer averages
        int footRow = sRow;
        wsSummary.Cell(footRow, 1).Value = "AVG";
        wsSummary.Cell(footRow, 1).Style.Font.Bold = true;
        if (rates.Any())
        {
            wsSummary.Cell(footRow, 11).Value = rates.Average(r => r.Rate) / 100.0;
            wsSummary.Cell(footRow, 11).Style.NumberFormat.Format = "0.0%";
            wsSummary.Cell(footRow, 11).Style.Font.Bold = true;
            var withExam = rates.Where(r => r.LatestExamScore.HasValue).ToList();
            if (withExam.Any())
            {
                wsSummary.Cell(footRow, 12).Value = (double)withExam.Average(r => r.LatestExamScore!.Value);
                wsSummary.Cell(footRow, 12).Style.NumberFormat.Format = "0.#";
                wsSummary.Cell(footRow, 12).Style.Font.Bold = true;
            }
            var withProj = rates.Where(r => r.ProjectRate.HasValue).ToList();
            if (withProj.Any())
            {
                wsSummary.Cell(footRow, 13).Value = withProj.Average(r => r.ProjectRate!.Value) / 100.0;
                wsSummary.Cell(footRow, 13).Style.NumberFormat.Format = "0.0%";
                wsSummary.Cell(footRow, 13).Style.Font.Bold = true;
            }
            wsSummary.Cell(footRow, 14).Value = (double)rates.Sum(r => r.TotalPoints);
            wsSummary.Cell(footRow, 14).Style.NumberFormat.Format = "#,##0.##";
            wsSummary.Cell(footRow, 14).Style.Font.Bold = true;
        }
        wsSummary.Range(footRow, 1, footRow, sumHeaders.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#e8eaf6");

        int[] colWidths = { 5, 26, 16, 12, 12, 9, 8, 10, 9, 9, 14, 12, 12, 13 };
        for (int i = 0; i < colWidths.Length; i++) wsSummary.Column(i + 1).Width = colWidths[i];
        wsSummary.SheetView.FreezeRows(4);

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
                "TooLate" => XLColor.FromHtml("#ffe0b2"),
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

    // ─── Yearly Archive Export ──────────────────────────────────────────────

    public Task<byte[]> ExportYearArchiveAsync(YearlyArchiveDetailDto archive)
    {
        using var wb = new XLWorkbook();

        // ────────────────────────────────────────────────────────────────────
        // Sheet 1 — Summary
        // ────────────────────────────────────────────────────────────────────
        var wsSummary = wb.Worksheets.Add("Summary");
        AddLogoRow(wsSummary, $"Year Archive — {archive.ArchiveYear}");
        wsSummary.Range(1, 1, 1, 4).Merge();

        int r = 3;
        void SummaryRow(string label, string value)
        {
            wsSummary.Cell(r, 1).Value = label;
            wsSummary.Cell(r, 1).Style.Font.Bold = true;
            wsSummary.Cell(r, 2).Value = value;
            r++;
        }
        SummaryRow("Academic Year",  archive.ArchiveYear);
        SummaryRow("Archived At",    archive.ArchivedAt.ToString("yyyy-MM-dd HH:mm") + " UTC");
        SummaryRow("Archived By",    archive.ArchivedBy);
        SummaryRow("Total Members",  archive.TotalMembers.ToString());
        SummaryRow("Total Groups",   archive.TotalGroups.ToString());
        wsSummary.Columns().AdjustToContents();

        // ────────────────────────────────────────────────────────────────────
        // Sheet 2 — All Members (combined across all groups)
        // ────────────────────────────────────────────────────────────────────
        BuildMembersSheet(wb, archive.Members, $"All Members — {archive.ArchiveYear}",
            "All Members", archiveYear: archive.ArchiveYear);

        // ────────────────────────────────────────────────────────────────────
        // Sheets 3..N — One sheet per Group
        // ────────────────────────────────────────────────────────────────────
        var groups = archive.Members
            .GroupBy(m => new { m.GroupId, m.GroupName })
            .OrderBy(g => g.Key.GroupName)
            .ToList();

        foreach (var grp in groups)
        {
            // Excel sheet names ≤ 31 chars, no special chars
            var sheetName = SafeSheetName(grp.Key.GroupName);
            BuildMembersSheet(wb, grp.ToList(),
                $"{grp.Key.GroupName} — {archive.ArchiveYear}",
                sheetName, archiveYear: archive.ArchiveYear);
        }

        return Task.FromResult(WorkbookToBytes(wb));
    }

    // ─── Per-group / all-members sheet builder ──────────────────────────────

    private static void BuildMembersSheet(
        XLWorkbook wb,
        List<YearlyMemberArchiveDto> members,
        string title,
        string sheetName,
        string archiveYear)
    {
        var ws = wb.Worksheets.Add(sheetName);
        int colCount = 11;
        AddLogoRow(ws, title);
        ws.Range(1, 1, 1, colCount).Merge();

        // ── Column headers ──
        var headers = new[]
        {
            "#", "Member Name", "Troop", "Grade",
            "Total Points",
            "Attendance %", "Events Attended / Total",
            "Exam Score (/100)",
            "Projects % ", "Projects Done / Total",
            "Excuses"
        };
        var hRow = ws.Row(3);
        for (int i = 0; i < headers.Length; i++) hRow.Cell(i + 1).Value = headers[i];
        StyleHeader(hRow, headers.Length);

        // ── Data rows ──
        int row = 4, rank = 1;
        foreach (var m in members.OrderByDescending(x => x.TotalPointsAtYearEnd))
        {
            bool alt = row % 2 == 0;
            var rowBg = XLColor.FromHtml(alt ? "#f3f4f9" : "#ffffff");

            ws.Cell(row, 1).Value  = rank++;
            ws.Cell(row, 2).Value  = m.MemberName;
            ws.Cell(row, 3).Value  = m.TroopName ?? "—";
            ws.Cell(row, 4).Value  = m.AcademicGrade ?? "—";

            // Points
            ws.Cell(row, 5).Value  = (double)m.TotalPointsAtYearEnd;
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.##";
            ws.Cell(row, 5).Style.Font.Bold = true;

            // Attendance %
            if (m.AttendanceRate.HasValue)
            {
                ws.Cell(row, 6).Value = (double)m.AttendanceRate.Value / 100.0;
                ws.Cell(row, 6).Style.NumberFormat.Format = "0.0%";
                var attColor = m.AttendanceRate.Value >= 80 ? "#e8f5e9"
                             : m.AttendanceRate.Value >= 60 ? "#fff3e0" : "#fce4ec";
                ws.Cell(row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml(attColor);
            }
            else { ws.Cell(row, 6).Value = "—"; }

            ws.Cell(row, 7).Value  = $"{m.TotalEventsAttended} / {m.TotalAttendanceCount}";

            // Exam score
            if (m.LatestExamScore.HasValue)
            {
                ws.Cell(row, 8).Value = (double)m.LatestExamScore.Value;
                ws.Cell(row, 8).Style.NumberFormat.Format = "0.##";
                var examColor = m.LatestExamScore.Value >= 80 ? "#e8f5e9"
                              : m.LatestExamScore.Value >= 60 ? "#fff3e0" : "#fce4ec";
                ws.Cell(row, 8).Style.Fill.BackgroundColor = XLColor.FromHtml(examColor);
            }
            else { ws.Cell(row, 8).Value = "—"; }

            // Projects %
            if (m.ProjectRate.HasValue)
            {
                ws.Cell(row, 9).Value = (double)m.ProjectRate.Value / 100.0;
                ws.Cell(row, 9).Style.NumberFormat.Format = "0.0%";
                var projColor = m.ProjectRate.Value >= 80 ? "#e8f5e9"
                              : m.ProjectRate.Value >= 60 ? "#fff3e0" : "#fce4ec";
                ws.Cell(row, 9).Style.Fill.BackgroundColor = XLColor.FromHtml(projColor);
            }
            else { ws.Cell(row, 9).Value = "—"; }

            ws.Cell(row, 10).Value = $"{m.ProjectsCompleted} / {m.TotalProjects}";
            ws.Cell(row, 11).Value = m.TotalExcusesCount;

            // Shade non-colored cells
            foreach (int c in new[] { 1, 2, 3, 4, 5, 7, 10, 11 })
                ws.Cell(row, c).Style.Fill.BackgroundColor = rowBg;

            row++;
        }

        // ── Footer totals ──
        int foot = row;
        ws.Cell(foot, 1).Value = "TOTAL / AVG";
        ws.Cell(foot, 1).Style.Font.Bold = true;
        ws.Cell(foot, 2).Value = $"{members.Count} members";
        ws.Cell(foot, 5).Value = members.Sum(m => (double)m.TotalPointsAtYearEnd);
        ws.Cell(foot, 5).Style.NumberFormat.Format = "#,##0.##";
        ws.Cell(foot, 5).Style.Font.Bold = true;

        var withAtt  = members.Where(m => m.AttendanceRate.HasValue).ToList();
        var withExam = members.Where(m => m.LatestExamScore.HasValue).ToList();
        var withProj = members.Where(m => m.ProjectRate.HasValue).ToList();

        if (withAtt.Any())
        {
            ws.Cell(foot, 6).Value = (double)withAtt.Average(m => m.AttendanceRate!.Value) / 100.0;
            ws.Cell(foot, 6).Style.NumberFormat.Format = "0.0%";
        }
        if (withExam.Any())
        {
            ws.Cell(foot, 8).Value = (double)withExam.Average(m => m.LatestExamScore!.Value);
            ws.Cell(foot, 8).Style.NumberFormat.Format = "0.##";
        }
        if (withProj.Any())
        {
            ws.Cell(foot, 9).Value = (double)withProj.Average(m => m.ProjectRate!.Value) / 100.0;
            ws.Cell(foot, 9).Style.NumberFormat.Format = "0.0%";
        }

        ws.Range(foot, 1, foot, headers.Length)
          .Style.Fill.BackgroundColor = XLColor.FromHtml("#e8eaf6");
        ws.Range(foot, 1, foot, headers.Length).Style.Font.Bold = true;

        int[] widths = { 4, 26, 16, 12, 13, 14, 22, 18, 14, 20, 9 };
        for (int i = 0; i < widths.Length; i++)
            ws.Column(i + 1).Width = widths[i];

        ws.SheetView.FreezeRows(3);
    }

    private static string SafeSheetName(string name)
    {
        var invalid = new[] { '/', '\\', '?', '*', '[', ']', ':' };
        var clean   = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        return clean.Length > 31 ? clean[..31] : clean;
    }

    // ─── Project Results Export ─────────────────────────────────────────────

    public Task<byte[]> ExportProjectResultsAsync(
        ProjectDto project, IEnumerable<ProjectMemberScoreDto> members)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Results");

        AddLogoRow(ws, $"Project Results — {project.Name}");
        ws.Range(1, 1, 1, 7).Merge();

        // Sub-header
        ws.Cell(2, 1).Value = $"Max Score: {project.MaxScore}  |  Group: {project.GroupName}" +
                              (project.TroopName != null ? $"  |  Troop: {project.TroopName}" : string.Empty);
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Range(2, 1, 2, 7).Merge();

        var headers = new[] { "Rank", "Member Name", "ID", "Troop", "Score", "Max", "%", "Grade" };
        var hRow = ws.Row(3);
        for (int i = 0; i < headers.Length; i++) hRow.Cell(i + 1).Value = headers[i];
        StyleHeader(hRow, headers.Length);

        var sorted = members
            .Where(m => m.IsGraded)
            .OrderByDescending(m => m.Score)
            .ToList();

        int row = 4, rank = 1;
        foreach (var m in sorted)
        {
            ws.Cell(row, 1).Value = rank++;
            ws.Cell(row, 2).Value = m.MemberName;
            ws.Cell(row, 3).Value = m.CustomId;
            ws.Cell(row, 4).Value = m.TroopName ?? "—";
            ws.Cell(row, 5).Value = (double)(m.Score ?? 0);
            ws.Cell(row, 6).Value = (double)project.MaxScore;
            ws.Cell(row, 7).Value = m.Percentage.HasValue ? $"{m.Percentage:F1}%" : "—";
            ws.Cell(row, 8).Value = $"{m.Grade} ({m.GradeArabic})";
            if (row % 2 == 0)
                ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f4f9");
            row++;
        }

        // Ungraded members
        var ungraded = members.Where(m => !m.IsGraded).OrderBy(m => m.MemberName).ToList();
        if (ungraded.Any())
        {
            ws.Cell(row, 1).Value = "— Not Yet Graded —";
            ws.Range(row, 1, row, headers.Length).Merge();
            ws.Cell(row, 1).Style.Font.Italic = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#fff3e0");
            row++;
            foreach (var m in ungraded)
            {
                ws.Cell(row, 1).Value = "—";
                ws.Cell(row, 2).Value = m.MemberName;
                ws.Cell(row, 3).Value = m.CustomId;
                ws.Cell(row, 4).Value = m.TroopName ?? "—";
                row++;
            }
        }

        // Average row
        if (sorted.Any())
        {
            double avg = sorted.Average(m => m.Percentage ?? 0);
            ws.Cell(row, 1).Value = "Class Average";
            ws.Range(row, 1, row, 6).Merge();
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 7).Value = $"{avg:F1}%";
            ws.Cell(row, 8).Value = ProjectService.GetGrade(avg) + " (" + ProjectService.GetGradeArabic(avg) + ")";
            ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#e8eaf6");
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(3);
        return Task.FromResult(WorkbookToBytes(wb));
    }

    /// <summary>Stub — reserved for future final-report export implementation.</summary>
    public Task<byte[]> ExportFinalReportAsync(object results)
        => throw new NotImplementedException("Final report export is not yet implemented.");
}
