using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Application.Services;
using ScoutsAttendance.Domain.Entities;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Infrastructure.Services;

/// <summary>
/// Generates Excel (.xlsx) and PDF exports for a single trip.
/// Excel uses ClosedXML (already in the project).
/// PDF uses QuestPDF Community (already licensed at startup).
/// </summary>
public class TripExportService : ITripExportService
{
    private readonly IUnitOfWork _uow;

    public TripExportService(IUnitOfWork uow) => _uow = uow;

    // ─── Brand colours ────────────────────────────────────────────────────────
    private const string PrimaryDark = "#1a237e";
    private const string SubHeader   = "#e8eaf6";
    private const string RowAlt      = "#f5f5f5";
    private const string White       = "#ffffff";
    private const string TextMuted   = "#9e9e9e";
    private const string GreenBg     = "#e8f5e9";
    private const string RedBg       = "#fce4ec";
    private const string OrangeBg    = "#fff3e0";
    private const string BlueBg      = "#e3f2fd";

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string SafeName(string name) =>
        string.Concat(name.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');

    private static string StatusLabel(int s) => s switch
    {
        0 => "Present",
        2 => "Late",
        3 => "Excused",
        _ => "Absent"
    };

    private static string StatusExcelColor(int s) => s switch
    {
        0 => "#e8f5e9",
        2 => "#fff3e0",
        3 => "#e3f2fd",
        _ => "#fce4ec"
    };

    // ─── Data loading ─────────────────────────────────────────────────────────

    private async Task<(Trip Trip,
                        List<TripBooking> Confirmed,
                        List<TripBooking> Waiting,
                        List<TripAttendanceRecord> Attendance)>
        LoadAsync(Guid tripId)
    {
        var trip = await _uow.Trips.Query()
            .Include(t => t.Group)
            .Include(t => t.Bookings)
                .ThenInclude(b => b.Member)
                    .ThenInclude(m => m!.Troop)
            .FirstOrDefaultAsync(t => t.Id == tripId && !t.IsDeleted)
            ?? throw new InvalidOperationException("Trip not found.");

        var attendance = await _uow.TripAttendanceRecords.Query()
            .Include(r => r.Member).ThenInclude(m => m!.Troop)
            .Where(r => r.TripId == tripId && !r.IsDeleted)
            .OrderBy(r => r.Member!.FirstName).ThenBy(r => r.Member!.LastName)
            .ToListAsync();

        var confirmed = trip.Bookings
            .Where(b => !b.IsDeleted && b.BookingStatus == BookingStatus.Confirmed)
            .OrderBy(b => b.Member?.FirstName).ThenBy(b => b.Member?.LastName)
            .ToList();

        var waiting = trip.Bookings
            .Where(b => !b.IsDeleted && b.BookingStatus == BookingStatus.Waiting)
            .OrderBy(b => b.CreatedAt)
            .ToList();

        return (trip, confirmed, waiting, attendance);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Excel
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<(byte[] Bytes, string Filename)> ExportExcelAsync(Guid tripId)
    {
        var (trip, confirmed, waiting, attendance) = await LoadAsync(tripId);
        var filename = $"Trip-{SafeName(trip.Name)}-{trip.TripDate:yyyy-MM-dd}.xlsx";

        using var wb = new XLWorkbook();

        BuildInfoSheet(wb, trip, confirmed, waiting);
        BuildBookingsSheet(wb, "Confirmed Bookings", confirmed);
        if (waiting.Any())
            BuildBookingsSheet(wb, "Waiting List", waiting);
        if (attendance.Any())
            BuildAttendanceSheet(wb, attendance, trip);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return (ms.ToArray(), filename);
    }

    // ── Sheet 1: Trip Info ────────────────────────────────────────────────────

    private static void BuildInfoSheet(XLWorkbook wb, Trip trip,
        List<TripBooking> confirmed, List<TripBooking> waiting)
    {
        var ws = wb.Worksheets.Add("Trip Info");

        // Title row
        ws.Row(1).Height = 32;
        var tc = ws.Cell(1, 1);
        tc.Value = $"Trip — {trip.Name}";
        tc.Style.Font.Bold = true;
        tc.Style.Font.FontSize = 15;
        tc.Style.Font.FontColor = XLColor.FromHtml(PrimaryDark);
        tc.Style.Fill.BackgroundColor = XLColor.FromHtml(SubHeader);
        tc.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Range(1, 1, 1, 2).Merge();

        var totalExpected = confirmed.Sum(b => b.AmountDue);
        var totalPaid     = confirmed.Where(b => b.PaidAt.HasValue).Sum(b => b.AmountDue);

        var rows = new List<(string Label, string Value)>
        {
            ("Trip Name",      trip.Name),
            ("Date",           trip.TripDate.ToString("dd/MM/yyyy")),
            ("Location",       trip.Location),
            ("Full Price",     $"{trip.Price:N0} EGP"),
            ("Sibling Price",  $"{trip.SiblingPrice:N0} EGP"),
            ("Capacity",       trip.MaxCapacity.HasValue ? trip.MaxCapacity.Value.ToString() : "Unlimited"),
            ("Confirmed",      confirmed.Count.ToString()),
            ("Waiting",        waiting.Count.ToString()),
            ("Total Expected", $"{totalExpected:N0} EGP"),
            ("Total Paid",     $"{totalPaid:N0} EGP"),
            ("Total Unpaid",   $"{(totalExpected - totalPaid):N0} EGP"),
        };
        if (trip.HasPoints && trip.PointValue.HasValue)
            rows.Add(("Points / Member", trip.PointValue.Value.ToString()));

        // Column headers
        ws.Cell(3, 1).Value = "Field";
        ws.Cell(3, 2).Value = "Value";
        ApplyHeaderStyle(ws.Row(3), 2);

        int row = 4;
        foreach (var (label, value) in rows)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = value;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml(SubHeader);
            row++;
        }

        ws.Column(1).Width = 22;
        ws.Column(2).Width = 30;
    }

    // ── Sheet 2 / 3: Bookings ─────────────────────────────────────────────────

    private static void BuildBookingsSheet(XLWorkbook wb, string sheetName,
        List<TripBooking> bookings)
    {
        var ws = wb.Worksheets.Add(sheetName);

        // Title
        ws.Row(1).Height = 28;
        var tc = ws.Cell(1, 1);
        tc.Value = sheetName;
        tc.Style.Font.Bold = true;
        tc.Style.Font.FontSize = 13;
        tc.Style.Font.FontColor = XLColor.FromHtml(PrimaryDark);
        tc.Style.Fill.BackgroundColor = XLColor.FromHtml(SubHeader);
        ws.Range(1, 1, 1, 9).Merge();

        // Headers
        var headers = new[]
        {
            "#", "Member Name", "Member ID", "Troop",
            "Sibling?", "Amount (EGP)", "Payment Status", "Paid Date", "Notes"
        };
        for (int i = 0; i < headers.Length; i++)
            ws.Row(3).Cell(i + 1).Value = headers[i];
        ApplyHeaderStyle(ws.Row(3), headers.Length);

        // Data
        int row = 4;
        decimal total = 0;
        foreach (var b in bookings)
        {
            bool isEven = (row % 2) == 0;
            var rowBg = XLColor.FromHtml(isEven ? RowAlt : White);

            ws.Cell(row, 1).Value = row - 3;
            ws.Cell(row, 2).Value = b.Member?.FullName ?? "";
            ws.Cell(row, 3).Value = b.Member?.CustomId.ToString("D6") ?? "";
            ws.Cell(row, 4).Value = b.Member?.Troop?.Name ?? "";
            ws.Cell(row, 5).Value = b.IsSibling ? "Yes" : "No";
            ws.Cell(row, 6).Value = (double)b.AmountDue;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";

            ws.Cell(row, 7).Value = b.PaidAt.HasValue ? "Paid" : "Unpaid";
            ws.Cell(row, 7).Style.Fill.BackgroundColor = b.PaidAt.HasValue
                ? XLColor.FromHtml(GreenBg) : XLColor.FromHtml(RedBg);

            ws.Cell(row, 8).Value = b.PaidAt.HasValue
                ? b.PaidAt.Value.ToString("dd/MM/yyyy") : "";
            ws.Cell(row, 9).Value = b.Notes;

            // Shade columns without special color
            foreach (int c in new[] { 1, 2, 3, 4, 5, 6, 8, 9 })
                ws.Cell(row, c).Style.Fill.BackgroundColor = rowBg;

            total += b.AmountDue;
            row++;
        }

        // Footer total
        ws.Cell(row, 5).Value = "TOTAL";
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Cell(row, 6).Value = (double)total;
        ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
        ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.FromHtml(SubHeader);

        // Column widths
        int[] widths = { 5, 26, 12, 18, 10, 14, 15, 13, 25 };
        for (int i = 0; i < widths.Length; i++)
            ws.Column(i + 1).Width = widths[i];
    }

    // ── Sheet 4: Attendance ───────────────────────────────────────────────────

    private static void BuildAttendanceSheet(XLWorkbook wb,
        List<TripAttendanceRecord> records, Trip trip)
    {
        var ws = wb.Worksheets.Add("Attendance");

        bool hasPoints = trip.HasPoints && trip.PointValue.HasValue;
        int cols = hasPoints ? 5 : 4;

        // Title
        ws.Row(1).Height = 28;
        var tc = ws.Cell(1, 1);
        tc.Value = "Attendance";
        tc.Style.Font.Bold = true;
        tc.Style.Font.FontSize = 13;
        tc.Style.Font.FontColor = XLColor.FromHtml(PrimaryDark);
        tc.Style.Fill.BackgroundColor = XLColor.FromHtml(SubHeader);
        ws.Range(1, 1, 1, cols).Merge();

        // Headers
        var headers = hasPoints
            ? new[] { "Member Name", "Member ID", "Troop", "Status", "Points Earned" }
            : new[] { "Member Name", "Member ID", "Troop", "Status" };
        for (int i = 0; i < headers.Length; i++)
            ws.Row(3).Cell(i + 1).Value = headers[i];
        ApplyHeaderStyle(ws.Row(3), headers.Length);

        // Data
        int row = 4;
        foreach (var r in records)
        {
            ws.Cell(row, 1).Value = r.Member?.FullName ?? "";
            ws.Cell(row, 2).Value = r.Member?.CustomId.ToString("D6") ?? "";
            ws.Cell(row, 3).Value = r.Member?.Troop?.Name ?? "";
            ws.Cell(row, 4).Value = StatusLabel(r.Status);
            ws.Cell(row, 4).Style.Fill.BackgroundColor =
                XLColor.FromHtml(StatusExcelColor(r.Status));

            if (hasPoints)
                ws.Cell(row, 5).Value = r.Status == 0 ? trip.PointValue!.Value : 0;

            row++;
        }

        int[] widths = hasPoints
            ? new[] { 26, 12, 18, 12, 13 }
            : new[] { 26, 12, 18, 12 };
        for (int i = 0; i < widths.Length; i++)
            ws.Column(i + 1).Width = widths[i];
    }

    // ── Shared Excel helpers ──────────────────────────────────────────────────

    private static void ApplyHeaderStyle(IXLRow row, int colCount)
    {
        var range = row.Worksheet.Range(row.RowNumber(), 1, row.RowNumber(), colCount);
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(PrimaryDark);
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Font.Bold = true;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PDF
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<(byte[] Bytes, string Filename)> ExportPdfAsync(
        Guid tripId, string exportedBy)
    {
        var (trip, confirmed, waiting, attendance) = await LoadAsync(tripId);
        var filename = $"Trip-{SafeName(trip.Name)}-{trip.TripDate:yyyy-MM-dd}.pdf";

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                // ── Header band ──────────────────────────────────────────────
                page.Header()
                    .Background(PrimaryDark)
                    .Padding(12)
                    .Column(col =>
                    {
                        col.Item()
                            .Text(trip.Name)
                            .Bold().FontSize(18).FontColor(White);
                        col.Item()
                            .Text($"{trip.TripDate:dd MMMM yyyy}  ·  {trip.Location}")
                            .FontSize(10).FontColor("#c5cae9");
                    });

                // ── Content ──────────────────────────────────────────────────
                page.Content()
                    .PaddingTop(14)
                    .Column(col =>
                    {
                        col.Spacing(12);

                        // Trip summary 4-cell grid
                        col.Item()
                            .Text("Trip Summary")
                            .Bold().FontSize(11).FontColor(PrimaryDark);

                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(); c.RelativeColumn();
                                c.RelativeColumn(); c.RelativeColumn();
                            });

                            void SC(string lbl, string val)
                            {
                                t.Cell().Background(SubHeader).Padding(8).Column(c =>
                                {
                                    c.Item().Text(lbl).FontSize(8).FontColor(TextMuted);
                                    c.Item().Text(val).FontSize(12).Bold().FontColor(PrimaryDark);
                                });
                            }

                            SC("Confirmed",      confirmed.Count.ToString());
                            SC("Waiting",        waiting.Count.ToString());
                            SC("Total Expected", $"{confirmed.Sum(b => b.AmountDue):N0} EGP");
                            SC("Total Paid",     $"{confirmed.Where(b => b.PaidAt.HasValue).Sum(b => b.AmountDue):N0} EGP");
                            SC("Full Price",     $"{trip.Price:N0} EGP");
                            SC("Sibling Price",  $"{trip.SiblingPrice:N0} EGP");
                            SC("Capacity",       trip.MaxCapacity.HasValue ? trip.MaxCapacity.Value.ToString() : "Unlimited");
                            SC("Points/Member",  trip.HasPoints ? (trip.PointValue?.ToString() ?? "0") : "N/A");
                        });

                        // ── Confirmed Bookings table ──────────────────────
                        if (confirmed.Any())
                        {
                            col.Item()
                                .Text($"Confirmed Bookings ({confirmed.Count})")
                                .Bold().FontSize(11).FontColor("#2e7d32");

                            col.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.ConstantColumn(18);  // #
                                    c.RelativeColumn(3);   // Name
                                    c.ConstantColumn(52);  // ID
                                    c.RelativeColumn(2);   // Troop
                                    c.ConstantColumn(38);  // Sibling
                                    c.ConstantColumn(55);  // Amount
                                    c.ConstantColumn(42);  // Status
                                });

                                t.Header(h =>
                                {
                                    foreach (var hdr in new[] { "#", "Member Name", "ID", "Troop", "Sibling", "Amount", "Status" })
                                        h.Cell().Background(PrimaryDark).Padding(4)
                                            .Text(hdr).Bold().FontSize(8).FontColor(White);
                                });

                                int idx = 1;
                                decimal total = 0;
                                foreach (var b in confirmed)
                                {
                                    var bg = idx % 2 == 0 ? RowAlt : White;
                                    t.Cell().Background(bg).Padding(3).Text(idx.ToString()).FontSize(8);
                                    t.Cell().Background(bg).Padding(3).Text(b.Member?.FullName ?? "").FontSize(8);
                                    t.Cell().Background(bg).Padding(3).Text(b.Member?.CustomId.ToString("D6") ?? "").FontSize(8);
                                    t.Cell().Background(bg).Padding(3).Text(b.Member?.Troop?.Name ?? "").FontSize(8);
                                    t.Cell().Background(bg).Padding(3).Text(b.IsSibling ? "Yes" : "No").FontSize(8);
                                    t.Cell().Background(bg).Padding(3).Text($"{b.AmountDue:N0}").FontSize(8);
                                    var paidBg = b.PaidAt.HasValue ? GreenBg : RedBg;
                                    t.Cell().Background(paidBg).Padding(3).Text(b.PaidAt.HasValue ? "Paid" : "Unpaid").FontSize(8);
                                    idx++;
                                    total += b.AmountDue;
                                }

                                // Total row — 7 cells
                                t.Cell().Background(SubHeader).Padding(3).Text("").FontSize(8);
                                t.Cell().Background(SubHeader).Padding(3).Text("TOTAL").Bold().FontSize(8);
                                t.Cell().Background(SubHeader).Padding(3).Text("").FontSize(8);
                                t.Cell().Background(SubHeader).Padding(3).Text("").FontSize(8);
                                t.Cell().Background(SubHeader).Padding(3).Text("").FontSize(8);
                                t.Cell().Background(SubHeader).Padding(3).Text($"{total:N0} EGP").Bold().FontSize(8);
                                t.Cell().Background(SubHeader).Padding(3).Text("").FontSize(8);
                            });
                        }

                        // ── Waiting List table ───────────────────────────
                        if (waiting.Any())
                        {
                            col.Item()
                                .Text($"Waiting List ({waiting.Count})")
                                .Bold().FontSize(11).FontColor("#e65100");

                            col.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.ConstantColumn(18);
                                    c.RelativeColumn(3);
                                    c.ConstantColumn(52);
                                    c.RelativeColumn(2);
                                    c.ConstantColumn(38);
                                    c.ConstantColumn(55);
                                });

                                t.Header(h =>
                                {
                                    foreach (var hdr in new[] { "#", "Member Name", "ID", "Troop", "Sibling", "Amount" })
                                        h.Cell().Background("#e65100").Padding(4)
                                            .Text(hdr).Bold().FontSize(8).FontColor(White);
                                });

                                int idx = 1;
                                foreach (var b in waiting)
                                {
                                    var bg = idx % 2 == 0 ? RowAlt : White;
                                    t.Cell().Background(bg).Padding(3).Text(idx.ToString()).FontSize(8);
                                    t.Cell().Background(bg).Padding(3).Text(b.Member?.FullName ?? "").FontSize(8);
                                    t.Cell().Background(bg).Padding(3).Text(b.Member?.CustomId.ToString("D6") ?? "").FontSize(8);
                                    t.Cell().Background(bg).Padding(3).Text(b.Member?.Troop?.Name ?? "").FontSize(8);
                                    t.Cell().Background(bg).Padding(3).Text(b.IsSibling ? "Yes" : "No").FontSize(8);
                                    t.Cell().Background(bg).Padding(3).Text($"{b.AmountDue:N0}").FontSize(8);
                                    idx++;
                                }
                            });
                        }

                        // ── Attendance table ─────────────────────────────
                        if (attendance.Any())
                        {
                            bool hasPoints = trip.HasPoints && trip.PointValue.HasValue;
                            col.Item()
                                .Text($"Attendance ({attendance.Count})")
                                .Bold().FontSize(11).FontColor("#283593");

                            col.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(3);
                                    c.ConstantColumn(52);
                                    c.RelativeColumn(2);
                                    c.ConstantColumn(52);
                                    if (hasPoints) c.ConstantColumn(50);
                                });

                                t.Header(h =>
                                {
                                    var hdrs = hasPoints
                                        ? new[] { "Member Name", "ID", "Troop", "Status", "Points" }
                                        : new[] { "Member Name", "ID", "Troop", "Status" };
                                    foreach (var hdr in hdrs)
                                        h.Cell().Background(PrimaryDark).Padding(4)
                                            .Text(hdr).Bold().FontSize(8).FontColor(White);
                                });

                                int idx = 1;
                                foreach (var a in attendance)
                                {
                                    var bg = idx % 2 == 0 ? RowAlt : White;
                                    var statusBg = a.Status switch
                                    {
                                        0 => GreenBg,
                                        2 => OrangeBg,
                                        3 => BlueBg,
                                        _ => RedBg
                                    };
                                    t.Cell().Background(bg).Padding(3).Text(a.Member?.FullName ?? "").FontSize(8);
                                    t.Cell().Background(bg).Padding(3).Text(a.Member?.CustomId.ToString("D6") ?? "").FontSize(8);
                                    t.Cell().Background(bg).Padding(3).Text(a.Member?.Troop?.Name ?? "").FontSize(8);
                                    t.Cell().Background(statusBg).Padding(3).Text(StatusLabel(a.Status)).FontSize(8);
                                    if (hasPoints)
                                        t.Cell().Background(bg).Padding(3).Text(a.Status == 0 ? trip.PointValue!.Value.ToString() : "0").FontSize(8);
                                    idx++;
                                }
                            });
                        }
                    });

                // ── Footer ───────────────────────────────────────────────────
                page.Footer()
                    .PaddingTop(4)
                    .BorderTop(1).BorderColor("#e0e0e0")
                    .Row(r =>
                    {
                        r.RelativeItem()
                            .Text($"Exported by {exportedBy}  ·  {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC")
                            .FontSize(8).FontColor(TextMuted);

                        r.AutoItem().AlignRight().Text(x =>
                        {
                            x.Span("Page ").FontSize(8).FontColor(TextMuted);
                            x.CurrentPageNumber().FontSize(8).FontColor(TextMuted);
                            x.Span(" of ").FontSize(8).FontColor(TextMuted);
                            x.TotalPages().FontSize(8).FontColor(TextMuted);
                        });
                    });
            });
        }).GeneratePdf();

        return (pdfBytes, filename);
    }
}
