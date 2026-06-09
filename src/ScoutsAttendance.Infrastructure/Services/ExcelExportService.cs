using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ScoutsAttendance.Application.DTOs.Admin;
using ScoutsAttendance.Application.DTOs.ExamScores;
using ScoutsAttendance.Application.DTOs.Projects;
using ScoutsAttendance.Application.DTOs.Reports;
using ScoutsAttendance.Application.Services;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Infrastructure.Services;

/// <summary>Generates Excel (.xlsx) files using ClosedXML for all major data exports.</summary>
public class ExcelExportService : IExcelExportService
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public ExcelExportService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

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

        // Scope to the caller's group unless they are a SystemAdmin
        if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
            query = query.Where(m => m.GroupId == _currentUser.GroupId.Value);
        else if (_currentUser.HasTroopScope && _currentUser.TroopId.HasValue)
            query = query.Where(m => m.TroopId == _currentUser.TroopId.Value);

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

        // Scope to the caller's group unless they are a SystemAdmin
        if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
            query = query.Where(m => m.GroupId == _currentUser.GroupId.Value);
        else if (_currentUser.HasTroopScope && _currentUser.TroopId.HasValue)
            query = query.Where(m => m.TroopId == _currentUser.TroopId.Value);

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
        // ── 1. Load events in the requested date range (scoped to caller's group) ─
        var evQuery = _uow.Events.Query().Where(e => !e.IsDeleted);

        // Non-SystemAdmin users see only their own group's events
        if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
            evQuery = evQuery.Where(e => e.GroupId == _currentUser.GroupId.Value);

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

        // Non-SystemAdmin: further restrict to their own group
        if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
            membersQuery = membersQuery.Where(m => m.GroupId == _currentUser.GroupId.Value);

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

        // ── 5. Load latest exam score per member + config for max scores ──────────
        var allExamScores = await _uow.MemberExamScores.Query()
            .Where(e => memberIds.Contains(e.MemberId))
            .ToListAsync();

        var latestExamByMember = allExamScores
            .GroupBy(e => e.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.Year).First());

        // Load configs for each year that appears in the exam records
        var examYears = allExamScores.Select(e => e.Year).Distinct().ToList();
        var examConfigs = examYears.Count > 0
            ? await _uow.ExamScoreConfigs.Query()
                .Where(c => groupIds.Contains(c.GroupId) && examYears.Contains(c.Year) && !c.IsDeleted)
                .ToListAsync()
            : new List<Domain.Entities.ExamScoreConfig>();
        // key: (groupId, year)
        var examConfigMap = examConfigs.ToDictionary(c => (c.GroupId, c.Year));

        // Build per-member exam info record
        var examMap = latestExamByMember.ToDictionary(
            kv => kv.Key,
            kv => {
                var e = kv.Value;
                // find config for this member's group + year
                Domain.Entities.ExamScoreConfig? cfg = null;
                var member = members.FirstOrDefault(m => m.Id == kv.Key);
                if (member != null) examConfigMap.TryGetValue((member.GroupId, e.Year), out cfg);
                decimal maxTheory   = cfg?.TheoreticalMaxScore ?? 0m;
                decimal maxPractical = cfg?.PracticalMaxScore  ?? 0m;
                decimal totalMax    = maxTheory + maxPractical;
                decimal? pct = totalMax > 0
                    ? Math.Round(e.TotalScore / totalMax * 100m, 1) : (decimal?)null;
                return (
                    TotalScore:   (decimal?)e.TotalScore,
                    Theoretical:  (decimal?)e.TheoreticalScore,
                    TheoMax:      maxTheory > 0 ? (decimal?)maxTheory   : null,
                    Practical:    (decimal?)e.PracticalScore,
                    PracMax:      maxPractical > 0 ? (decimal?)maxPractical : null,
                    Percentage:   pct
                );
            });

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
                HasNeckerchief    = m.HasNeckerchief,
                TotalEvents       = total,
                Present           = present,
                Late              = late,
                TooLate           = tooLate,
                Excused           = excused,
                Absent            = absent,
                LatestExamScore          = examMap.TryGetValue(m.Id, out var ex) ? ex.TotalScore    : null,
                LatestExamTheoretical    = ex.Theoretical,
                LatestExamTheoreticalMax = ex.TheoMax,
                LatestExamPractical      = ex.Practical,
                LatestExamPracticalMax   = ex.PracMax,
                LatestExamPercentage     = ex.Percentage,
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
        wsSummary.Range(1, 1, 1, 17).Merge();

        // Date range subtitle
        var rangeLabel = $"Period: {(from.HasValue ? from.Value.ToString("yyyy-MM-dd") : "All")} → {(to.HasValue ? to.Value.ToString("yyyy-MM-dd") : "All")}";
        wsSummary.Cell(2, 1).Value = rangeLabel;
        wsSummary.Cell(2, 1).Style.Font.Italic = true;
        wsSummary.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#555555");
        wsSummary.Range(2, 1, 2, 17).Merge();

        const int sumCols = 17;
        var sumHeaders = new[]
        {
            "Rank", "Member", "Troop", "Grade", "Foulard",
            "Total Events", "Present", "Late", "Too Late", "Excused", "Absent",
            "Attendance %", "Theory", "Practical", "Exam %", "Projects %", "Total Points"
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

            // Foulard (col 5)
            wsSummary.Cell(sRow,  5).Value = r.HasNeckerchief ? "Yes" : "No";
            wsSummary.Cell(sRow,  5).Style.Font.Bold = true;
            wsSummary.Cell(sRow,  5).Style.Fill.BackgroundColor = r.HasNeckerchief
                ? XLColor.FromHtml("#c8e6c9") : XLColor.FromHtml("#ffcdd2");

            wsSummary.Cell(sRow,  6).Value = r.TotalEvents;
            wsSummary.Cell(sRow,  7).Value = r.Present;
            wsSummary.Cell(sRow,  8).Value = r.Late;
            wsSummary.Cell(sRow,  9).Value = r.TooLate;
            wsSummary.Cell(sRow, 10).Value = r.Excused;
            wsSummary.Cell(sRow, 11).Value = r.Absent;

            // Attendance %
            wsSummary.Cell(sRow, 12).Value = r.Rate / 100.0;
            wsSummary.Cell(sRow, 12).Style.NumberFormat.Format = "0.0%";
            wsSummary.Cell(sRow, 12).Style.Fill.BackgroundColor = r.Rate switch
            {
                >= 90 => XLColor.FromHtml("#c8e6c9"),
                >= 70 => XLColor.FromHtml("#fff9c4"),
                >= 50 => XLColor.FromHtml("#ffe0b2"),
                _     => XLColor.FromHtml("#ffcdd2")
            };
            wsSummary.Cell(sRow, 12).Style.Font.Bold = true;

            // Theory (col 13)  e.g.  45 / 50
            if (r.LatestExamTheoretical.HasValue)
            {
                var theoLabel = r.LatestExamTheoreticalMax.HasValue
                    ? $"{r.LatestExamTheoretical.Value} / {r.LatestExamTheoreticalMax.Value}"
                    : r.LatestExamTheoretical.Value.ToString("0.##");
                wsSummary.Cell(sRow, 13).Value = theoLabel;
                wsSummary.Cell(sRow, 13).Style.Fill.BackgroundColor = XLColor.FromHtml("#e3f2fd");
            }
            else { wsSummary.Cell(sRow, 13).Value = "—"; }

            // Practical (col 14)  e.g.  40 / 50
            if (r.LatestExamPractical.HasValue)
            {
                var pracLabel = r.LatestExamPracticalMax.HasValue
                    ? $"{r.LatestExamPractical.Value} / {r.LatestExamPracticalMax.Value}"
                    : r.LatestExamPractical.Value.ToString("0.##");
                wsSummary.Cell(sRow, 14).Value = pracLabel;
                wsSummary.Cell(sRow, 14).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3e5f5");
            }
            else { wsSummary.Cell(sRow, 14).Value = "—"; }

            // Exam % (col 15)
            if (r.LatestExamPercentage.HasValue)
            {
                wsSummary.Cell(sRow, 15).Value = (double)r.LatestExamPercentage.Value / 100.0;
                wsSummary.Cell(sRow, 15).Style.NumberFormat.Format = "0.0%";
                wsSummary.Cell(sRow, 15).Style.Font.Bold = true;
                wsSummary.Cell(sRow, 15).Style.Fill.BackgroundColor = r.LatestExamPercentage.Value switch
                {
                    >= 90 => XLColor.FromHtml("#c8e6c9"),
                    >= 75 => XLColor.FromHtml("#fff9c4"),
                    >= 50 => XLColor.FromHtml("#ffe0b2"),
                    _     => XLColor.FromHtml("#ffcdd2")
                };
            }
            else { wsSummary.Cell(sRow, 15).Value = "—"; }

            // Projects % (col 16)
            if (r.ProjectRate.HasValue)
            {
                wsSummary.Cell(sRow, 16).Value = r.ProjectRate.Value / 100.0;
                wsSummary.Cell(sRow, 16).Style.NumberFormat.Format = "0.0%";
                wsSummary.Cell(sRow, 16).Style.Fill.BackgroundColor = r.ProjectRate.Value switch
                {
                    >= 80 => XLColor.FromHtml("#c8e6c9"),
                    >= 60 => XLColor.FromHtml("#fff9c4"),
                    _     => XLColor.FromHtml("#ffcdd2")
                };
            }
            else { wsSummary.Cell(sRow, 16).Value = "—"; }

            // Total Points (col 17)
            wsSummary.Cell(sRow, 17).Value = (double)r.TotalPoints;
            wsSummary.Cell(sRow, 17).Style.NumberFormat.Format = "#,##0.##";
            wsSummary.Cell(sRow, 17).Style.Font.Bold = true;

            // Row background for non-colour cells
            foreach (int c in new[] { 1, 2, 3, 4, 6, 7, 8, 9, 10, 11, 17 })
                wsSummary.Cell(sRow, c).Style.Fill.BackgroundColor = rowBg;

            sRow++;
        }

        // Footer averages
        int footRow = sRow;
        wsSummary.Cell(footRow, 1).Value = "AVG";
        wsSummary.Cell(footRow, 1).Style.Font.Bold = true;
        if (rates.Any())
        {
            // Avg Attendance % (col 12)
            wsSummary.Cell(footRow, 12).Value = rates.Average(r => r.Rate) / 100.0;
            wsSummary.Cell(footRow, 12).Style.NumberFormat.Format = "0.0%";
            wsSummary.Cell(footRow, 12).Style.Font.Bold = true;
            // Avg Exam % (col 15)
            var withExamPct = rates.Where(r => r.LatestExamPercentage.HasValue).ToList();
            if (withExamPct.Any())
            {
                wsSummary.Cell(footRow, 15).Value = (double)withExamPct.Average(r => r.LatestExamPercentage!.Value) / 100.0;
                wsSummary.Cell(footRow, 15).Style.NumberFormat.Format = "0.0%";
                wsSummary.Cell(footRow, 15).Style.Font.Bold = true;
            }
            // Avg Projects % (col 16)
            var withProj = rates.Where(r => r.ProjectRate.HasValue).ToList();
            if (withProj.Any())
            {
                wsSummary.Cell(footRow, 16).Value = withProj.Average(r => r.ProjectRate!.Value) / 100.0;
                wsSummary.Cell(footRow, 16).Style.NumberFormat.Format = "0.0%";
                wsSummary.Cell(footRow, 16).Style.Font.Bold = true;
            }
            // Total Points sum (col 17)
            wsSummary.Cell(footRow, 17).Value = (double)rates.Sum(r => r.TotalPoints);
            wsSummary.Cell(footRow, 17).Style.NumberFormat.Format = "#,##0.##";
            wsSummary.Cell(footRow, 17).Style.Font.Bold = true;
        }
        wsSummary.Range(footRow, 1, footRow, sumHeaders.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#e8eaf6");

        int[] colWidths = { 5, 26, 16, 12, 10, 9, 8, 8, 9, 9, 9, 13, 13, 13, 12, 13, 13 };
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

        // Scope to the caller's group unless they are a SystemAdmin
        if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
            query = query.Where(p => p.Member.GroupId == _currentUser.GroupId.Value);
        else if (_currentUser.HasTroopScope && _currentUser.TroopId.HasValue)
            query = query.Where(p => p.Member.TroopId == _currentUser.TroopId.Value);

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

        // Scope to the caller's group unless they are a SystemAdmin
        if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
            query = query.Where(p => p.Troop.GroupId == _currentUser.GroupId.Value);
        else if (_currentUser.HasTroopScope && _currentUser.TroopId.HasValue)
            query = query.Where(p => p.TroopId == _currentUser.TroopId.Value);

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

        // Load configs for percentage calculation
        var groupIds = scores.Select(s => s.Member?.Troop?.GroupId ?? Guid.Empty).Distinct().ToList();
        var years    = scores.Select(s => s.Year).Distinct().ToList();
        var cfgMap   = await LoadExamConfigsAsync(groupIds, years);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Exam Scores");

        const int colCount = 8;
        AddLogoRow(ws, "End-of-Year Exam Scores");
        ws.Range(1, 1, 1, colCount).Merge();

        var headers = new[] { "Member", "Troop", "Year", "Theory", "Practical", "Total", "% Score", "Grade", "Notes" };
        var hRow = ws.Row(3);
        for (int i = 0; i < headers.Length; i++) hRow.Cell(i + 1).Value = headers[i];
        StyleHeader(hRow, headers.Length);

        int row = 4;
        foreach (var s in scores)
        {
            var gid  = s.Member?.Troop?.GroupId ?? Guid.Empty;
            cfgMap.TryGetValue((gid, s.Year), out var cfg);
            decimal totalMax = cfg is not null ? cfg.TheoreticalMaxScore + cfg.PracticalMaxScore : 0m;
            decimal total    = s.TotalScore;
            decimal? pct     = totalMax > 0 ? Math.Round(total / totalMax * 100m, 2) : null;
            string   grade   = ExamScoreService.GetGrade((double)(pct ?? (totalMax > 0 ? 0 : total)));

            ws.Cell(row, 1).Value = s.Member?.FullName ?? "";
            ws.Cell(row, 2).Value = s.Member?.Troop?.Name ?? "";
            ws.Cell(row, 3).Value = s.Year;
            ws.Cell(row, 4).Value = (double)s.TheoreticalScore;
            ws.Cell(row, 5).Value = (double)s.PracticalScore;
            ws.Cell(row, 6).Value = (double)total;
            ws.Cell(row, 6).Style.Font.Bold = true;

            if (pct.HasValue)
            {
                ws.Cell(row, 7).Value = (double)pct.Value / 100.0;
                ws.Cell(row, 7).Style.NumberFormat.Format = "0.0%";
                ws.Cell(row, 7).Style.Fill.BackgroundColor = pct.Value >= 50
                    ? XLColor.FromHtml("#c8e6c9") : XLColor.FromHtml("#ffcdd2");
            }
            else { ws.Cell(row, 7).Value = "—"; }

            ws.Cell(row, 8).Value = grade;
            ws.Cell(row, 9).Value = s.Notes ?? "";

            if (row % 2 == 0)
                ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f4f9");
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(3);
        return WorkbookToBytes(wb);
    }

    // ─── Exam Score Template Export ────────────────────────────────────────────

    public async Task<byte[]> ExportExamScoreTemplateAsync(Guid groupId, int year, Guid? troopId = null)
    {
        // Load members in this group (optionally filtered by troop)
        var memberQuery = _uow.Members.Query()
            .Include(m => m.Troop)
            .Where(m => m.GroupId == groupId && !m.IsDeleted);
        if (troopId.HasValue) memberQuery = memberQuery.Where(m => m.TroopId == troopId.Value);
        var members = await memberQuery
            .OrderBy(m => m.Troop == null ? "" : m.Troop.Name)
            .ThenBy(m => m.LastName)
            .ToListAsync();

        // Load config so we can show max scores in header
        var cfg = await _uow.ExamScoreConfigs.Query()
            .FirstOrDefaultAsync(c => c.GroupId == groupId && c.Year == year && !c.IsDeleted);

        decimal theoMax = cfg?.TheoreticalMaxScore ?? 50m;
        decimal pracMax = cfg?.PracticalMaxScore   ?? 50m;
        decimal totMax  = theoMax + pracMax;

        // Load existing scores for this year so we can pre-fill them
        var memberIds = members.Select(m => m.Id).ToList();
        var existing = await _uow.MemberExamScores.Query()
            .Where(x => memberIds.Contains(x.MemberId) && x.Year == year && !x.IsDeleted)
            .ToListAsync();
        var scoreMap = existing.ToDictionary(x => x.MemberId);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Exam Scores");

        const int colCount = 7;

        // Title row
        AddLogoRow(ws, $"Exam Scores Entry Template — {year}");
        ws.Range(1, 1, 1, colCount).Merge();

        // Sub-header: max scores info
        ws.Cell(2, 1).Value = $"Theory Max: {theoMax}  |  Practical Max: {pracMax}  |  Total Max: {totMax}  |  Year: {year}";
        ws.Cell(2, 1).Style.Font.Italic  = true;
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#555555");
        ws.Range(2, 1, 2, colCount).Merge();

        // Instructions row
        ws.Cell(3, 1).Value = "Fill in Theoretical and Practical scores only. Do NOT change MemberID column. Leave blank to skip.";
        ws.Cell(3, 1).Style.Font.Italic = true;
        ws.Cell(3, 1).Style.Font.FontColor = XLColor.FromHtml("#c62828");
        ws.Range(3, 1, 3, colCount).Merge();

        // Header row
        var hRow = ws.Row(5);
        var headers = new[] { "MemberID", "Member Name", "Troop", $"Theory (/{theoMax})", $"Practical (/{pracMax})", $"Total (/{totMax})", "Notes" };
        for (int i = 0; i < headers.Length; i++) hRow.Cell(i + 1).Value = headers[i];
        StyleHeader(hRow, headers.Length);
        ws.SheetView.FreezeRows(5);

        int dataStartRow = 6;
        int row = dataStartRow;
        foreach (var m in members)
        {
            scoreMap.TryGetValue(m.Id, out var existingScore);

            ws.Cell(row, 1).Value = m.CustomId.ToString("D6");
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = m.FullName;
            ws.Cell(row, 3).Value = m.Troop?.Name ?? "—";

            if (existingScore is not null)
            {
                ws.Cell(row, 4).Value = (double)existingScore.TheoreticalScore;
                ws.Cell(row, 5).Value = (double)existingScore.PracticalScore;
            }
            // Col 6: Total formula  =D{row}+E{row}
            ws.Cell(row, 6).FormulaA1 = $"=D{row}+E{row}";
            ws.Cell(row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#f5f5f5");
            ws.Cell(row, 6).Style.Font.Italic = true;

            ws.Cell(row, 7).Value = existingScore?.Notes ?? "";

            // Lock MemberID column so it is obvious which column must not be changed
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#e8eaf6");
            ws.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#e8eaf6");
            ws.Cell(row, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#e8eaf6");

            if (row % 2 == 0)
                ws.Range(row, 1, row, colCount).Style.Fill.BackgroundColor = XLColor.FromHtml("#f9f9f9");

            row++;
        }

        // Set column widths
        ws.Column(1).Width = 12;
        ws.Column(2).Width = 28;
        ws.Column(3).Width = 18;
        ws.Column(4).Width = 18;
        ws.Column(5).Width = 18;
        ws.Column(6).Width = 16;
        ws.Column(7).Width = 24;

        return WorkbookToBytes(wb);
    }

    // ─── Exam Score Import ─────────────────────────────────────────────────────

    public async Task<ImportExamScoreResultDto> ImportExamScoresAsync(
        Stream file, Guid groupId, int year)
    {
        // Load config for validation
        var cfg = await _uow.ExamScoreConfigs.Query()
            .FirstOrDefaultAsync(c => c.GroupId == groupId && c.Year == year && !c.IsDeleted);

        decimal theoMax = cfg?.TheoreticalMaxScore ?? decimal.MaxValue;
        decimal pracMax  = cfg?.PracticalMaxScore   ?? decimal.MaxValue;

        // Load all members in this group (keyed by 6-digit CustomId)
        var members = await _uow.Members.Query()
            .Where(m => m.GroupId == groupId && !m.IsDeleted)
            .ToListAsync();
        var memberByCustomId = members.ToDictionary(m => m.CustomId);

        // Load existing scores for upsert
        var memberIds = members.Select(m => m.Id).ToList();
        var existingScores = await _uow.MemberExamScores.Query()
            .Where(x => memberIds.Contains(x.MemberId) && x.Year == year && !x.IsDeleted)
            .ToListAsync();
        var existingMap = existingScores.ToDictionary(x => x.MemberId);

        var skipped   = new List<ImportSkippedRowDto>();
        var toInsert  = new List<MemberExamScore>();
        var toUpdate  = new List<MemberExamScore>();

        using var wb = new XLWorkbook(file);
        var ws = wb.Worksheets.First();

        // Find header row: scan first 10 rows for "MemberID" in column 1
        int headerRow = 5; // default
        for (int r = 1; r <= 10; r++)
        {
            var val = ws.Cell(r, 1).GetString().Trim();
            if (val.Equals("MemberID", StringComparison.OrdinalIgnoreCase))
            {
                headerRow = r;
                break;
            }
        }

        int dataStart = headerRow + 1;
        int lastRow   = ws.LastRowUsed()?.RowNumber() ?? dataStart;

        for (int r = dataStart; r <= lastRow; r++)
        {
            var memberIdStr  = ws.Cell(r, 1).GetString().Trim();
            var theoStr      = ws.Cell(r, 4).GetString().Trim();
            var pracStr      = ws.Cell(r, 5).GetString().Trim();
            var notes        = ws.Cell(r, 7).GetString().Trim();

            // Skip completely blank rows
            if (string.IsNullOrEmpty(memberIdStr) && string.IsNullOrEmpty(theoStr) && string.IsNullOrEmpty(pracStr))
                continue;

            // Both score cells empty → skip (don't overwrite existing)
            if (string.IsNullOrEmpty(theoStr) && string.IsNullOrEmpty(pracStr))
            {
                skipped.Add(new ImportSkippedRowDto
                {
                    RowNumber  = r,
                    MemberId   = memberIdStr,
                    MemberName = ws.Cell(r, 2).GetString(),
                    Reason     = "Both score cells are empty — row skipped."
                });
                continue;
            }

            // Parse MemberID
            if (!int.TryParse(memberIdStr, out int customId))
            {
                skipped.Add(new ImportSkippedRowDto
                {
                    RowNumber  = r,
                    MemberId   = memberIdStr,
                    MemberName = ws.Cell(r, 2).GetString(),
                    Reason     = "Invalid or missing MemberID."
                });
                continue;
            }

            if (!memberByCustomId.TryGetValue(customId, out var member))
            {
                skipped.Add(new ImportSkippedRowDto
                {
                    RowNumber  = r,
                    MemberId   = memberIdStr,
                    MemberName = ws.Cell(r, 2).GetString(),
                    Reason     = $"Member #{customId:D6} not found in this group."
                });
                continue;
            }

            // Parse scores — treat empty as "keep existing"
            decimal? theoVal = null;
            decimal? pracVal = null;

            if (!string.IsNullOrEmpty(theoStr))
            {
                if (!decimal.TryParse(theoStr, out var t))
                {
                    skipped.Add(new ImportSkippedRowDto
                    {
                        RowNumber  = r,
                        MemberId   = memberIdStr,
                        MemberName = member.FullName,
                        Reason     = $"Invalid theoretical score value: '{theoStr}'."
                    });
                    continue;
                }
                if (t < 0 || t > theoMax)
                {
                    skipped.Add(new ImportSkippedRowDto
                    {
                        RowNumber  = r,
                        MemberId   = memberIdStr,
                        MemberName = member.FullName,
                        Reason     = $"Theoretical score {t} exceeds maximum {theoMax}."
                    });
                    continue;
                }
                theoVal = t;
            }

            if (!string.IsNullOrEmpty(pracStr))
            {
                if (!decimal.TryParse(pracStr, out var p))
                {
                    skipped.Add(new ImportSkippedRowDto
                    {
                        RowNumber  = r,
                        MemberId   = memberIdStr,
                        MemberName = member.FullName,
                        Reason     = $"Invalid practical score value: '{pracStr}'."
                    });
                    continue;
                }
                if (p < 0 || p > pracMax)
                {
                    skipped.Add(new ImportSkippedRowDto
                    {
                        RowNumber  = r,
                        MemberId   = memberIdStr,
                        MemberName = member.FullName,
                        Reason     = $"Practical score {p} exceeds maximum {pracMax}."
                    });
                    continue;
                }
                pracVal = p;
            }

            if (existingMap.TryGetValue(member.Id, out var existing))
            {
                // Update: only overwrite non-empty cells
                if (theoVal.HasValue) existing.TheoreticalScore = theoVal.Value;
                if (pracVal.HasValue) existing.PracticalScore   = pracVal.Value;
                if (!string.IsNullOrEmpty(notes)) existing.Notes = notes;
                existing.UpdatedAt = DateTime.UtcNow;
                toUpdate.Add(existing);
            }
            else
            {
                toInsert.Add(new MemberExamScore
                {
                    MemberId          = member.Id,
                    Year              = year,
                    TheoreticalScore  = theoVal ?? 0m,
                    PracticalScore    = pracVal ?? 0m,
                    Notes             = string.IsNullOrEmpty(notes) ? null : notes,
                    CreatedBy         = _currentUser.UserId
                });
            }
        }

        // Persist in a single transaction
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            foreach (var s in toUpdate)
                _uow.MemberExamScores.Update(s);
            foreach (var s in toInsert)
                await _uow.MemberExamScores.AddAsync(s);
            await _uow.SaveChangesAsync();
        });

        return new ImportExamScoreResultDto
        {
            ImportedCount = toInsert.Count + toUpdate.Count,
            SkippedCount  = skipped.Count,
            SkippedRows   = skipped
        };
    }

    // ─── Exam Score Config helper ──────────────────────────────────────────────

    private async Task<Dictionary<(Guid, int), ScoutsAttendance.Domain.Entities.ExamScoreConfig>> LoadExamConfigsAsync(
        List<Guid> groupIds, List<int> years)
    {
        if (groupIds.Count == 0 || years.Count == 0) return [];
        var list = await _uow.ExamScoreConfigs.Query()
            .Where(c => groupIds.Contains(c.GroupId) && years.Contains(c.Year) && !c.IsDeleted)
            .ToListAsync();
        return list.ToDictionary(c => (c.GroupId, c.Year));
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

    // ─── Badges Export ─────────────────────────────────────────────────────────

    public async Task<byte[]> ExportBadgesAsync(Guid? troopId, string? category, DateTime? from, DateTime? to)
    {
        // Load badge awards (scoped to caller's group for non-SystemAdmin)
        var query = _uow.MemberBadges.Query()
            .Include(mb => mb.Badge)
            .Include(mb => mb.Member).ThenInclude(m => m.Troop)
            .Include(mb => mb.Member).ThenInclude(m => m.Group)
            .Where(mb => !mb.IsDeleted && !mb.Member.IsDeleted);

        if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
            query = query.Where(mb => mb.Member.GroupId == _currentUser.GroupId.Value);

        if (troopId.HasValue)
            query = query.Where(mb => mb.TroopId == troopId.Value || mb.Member.TroopId == troopId.Value);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(mb => mb.Badge.Category == category);

        if (from.HasValue)
            query = query.Where(mb => mb.AwardedDate >= from.Value);

        if (to.HasValue)
            query = query.Where(mb => mb.AwardedDate <= to.Value.AddDays(1));

        var records = await query
            .OrderByDescending(mb => mb.AwardedDate)
            .ThenBy(mb => mb.Member.LastName)
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Badges");

        const int cols = 10;
        AddLogoRow(ws, "Badges Report — Scouts Attendance System");
        ws.Range(1, 1, 1, cols).Merge();

        // Sub-header: applied filters
        var filterParts = new List<string>();
        if (from.HasValue || to.HasValue)
            filterParts.Add($"Period: {(from.HasValue ? from.Value.ToString("yyyy-MM-dd") : "—")} → {(to.HasValue ? to.Value.ToString("yyyy-MM-dd") : "—")}");
        if (!string.IsNullOrEmpty(category)) filterParts.Add($"Category: {category}");
        ws.Cell(2, 1).Value = filterParts.Any() ? string.Join("  |  ", filterParts) : "All records";
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#555555");
        ws.Range(2, 1, 2, cols).Merge();

        // Headers
        var headers = new[]
        {
            "#", "Member Name", "Member ID", "Troop", "Group",
            "Badge", "Category", "Awarded Date", "Awarded By", "Notes"
        };
        var hRow = ws.Row(4);
        for (int i = 0; i < headers.Length; i++) hRow.Cell(i + 1).Value = headers[i];
        StyleHeader(hRow, headers.Length);

        int row = 5;
        foreach (var mb in records)
        {
            bool alt = row % 2 == 0;
            var bg = XLColor.FromHtml(alt ? "#f3f4f9" : "#ffffff");

            ws.Cell(row, 1).Value  = row - 4;
            ws.Cell(row, 2).Value  = mb.Member?.FullName ?? "";
            ws.Cell(row, 3).Value  = mb.Member?.CustomId.ToString("D6") ?? "";
            ws.Cell(row, 4).Value  = mb.TroopName ?? mb.Member?.Troop?.Name ?? "—";
            ws.Cell(row, 5).Value  = mb.GroupName ?? mb.Member?.Group?.Name ?? "—";
            ws.Cell(row, 6).Value  = mb.Badge?.Name ?? "";
            ws.Cell(row, 6).Style.Font.Bold = true;

            // Badge category with colour
            var cat = mb.Badge?.Category ?? "";
            ws.Cell(row, 7).Value = cat;
            if (!string.IsNullOrEmpty(cat))
            {
                var catColor = cat switch
                {
                    "Skills"     => "#e3f2fd",
                    "Community"  => "#e8f5e9",
                    "Sports"     => "#fff3e0",
                    "Leadership" => "#f3e5f5",
                    _            => "#f5f5f5"
                };
                ws.Cell(row, 7).Style.Fill.BackgroundColor = XLColor.FromHtml(catColor);
            }

            ws.Cell(row, 8).Value  = mb.AwardedDate.ToString("yyyy-MM-dd");
            ws.Cell(row, 9).Value  = mb.AwardedBy;
            ws.Cell(row, 10).Value = mb.Notes ?? "";

            foreach (int c in new[] { 1, 2, 3, 4, 5, 6, 8, 9, 10 })
                ws.Cell(row, c).Style.Fill.BackgroundColor = bg;
            row++;
        }

        // Footer total row
        int foot = row;
        ws.Cell(foot, 1).Value = $"Total: {records.Count} award{(records.Count != 1 ? "s" : "")}";
        ws.Cell(foot, 1).Style.Font.Bold = true;
        ws.Range(foot, 1, foot, cols).Style.Fill.BackgroundColor = XLColor.FromHtml("#e8eaf6");

        int[] widths = { 5, 26, 10, 16, 16, 20, 12, 14, 16, 22 };
        for (int i = 0; i < widths.Length; i++) ws.Column(i + 1).Width = widths[i];
        ws.SheetView.FreezeRows(4);

        return WorkbookToBytes(wb);
    }
}
