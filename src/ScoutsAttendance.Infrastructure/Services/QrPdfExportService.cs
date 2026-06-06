using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.Infrastructure.Services;

/// <summary>
/// Generates a print-ready A4 PDF of all member QR codes, grouped by Troop.
/// Arabic text is rendered via GDI+ (System.Drawing) on Windows, which applies
/// proper RTL ordering and Arabic character shaping — then embedded as a PNG.
/// </summary>
public sealed class QrPdfExportService : IQrPdfExportService
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IQrCodeService      _qrCode;

    private const double PageW   = 595;
    private const double PageH   = 842;
    private const double Margin  = 36;
    private const double Cols    = 3;
    private const double CardH   = 175;
    private const double HeaderH = 40;
    private const double FooterH = 22;

    private static readonly XColor ColPrimDark = XColor.FromArgb(0x1a, 0x23, 0x7e);
    private static readonly XColor ColPrimary  = XColor.FromArgb(0x3f, 0x51, 0xb5);
    private static readonly XColor ColText2    = XColor.FromArgb(0x5a, 0x63, 0x79);
    private static readonly XColor ColBorder   = XColor.FromArgb(0xe0, 0xe0, 0xe0);

    private static bool IsArabic(string? s) =>
        s != null && s.Any(c => c >= 0x0600 && c <= 0x06FF);

    private static bool OnWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Renders text as a transparent PNG via Windows GDI+.
    /// GDI+ correctly applies Arabic character shaping and RTL bidi ordering.
    /// </summary>
    private static byte[]? GdiRenderText(
        string text, float ptSize, bool bold,
        System.Drawing.Color fg, int pxW, int pxH)
    {
        if (!OnWindows || pxW < 1 || pxH < 1) return null;
        try
        {
            using var bmp = new Bitmap(pxW, pxH, PixelFormat.Format32bppArgb);
            using var gfx = Graphics.FromImage(bmp);
            gfx.Clear(System.Drawing.Color.Transparent);
            gfx.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            gfx.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            var style  = bold ? FontStyle.Bold : FontStyle.Regular;
            using var fnt    = new System.Drawing.Font("Arial", ptSize, style);
            using var brush  = new SolidBrush(fg);
            using var fmt    = new StringFormat(StringFormat.GenericDefault);
            fmt.Alignment     = StringAlignment.Center;
            fmt.LineAlignment = StringAlignment.Center;

            gfx.DrawString(text, fnt, brush, new RectangleF(0, 0, pxW, pxH), fmt);

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        catch { return null; }
    }

    public QrPdfExportService(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IQrCodeService qrCode)
    {
        _uow = uow; _currentUser = currentUser; _qrCode = qrCode;
    }

    public async Task<(byte[] Bytes, string Filename)> ExportAsync()
    {
        var query = _uow.Members.Query()
            .IgnoreQueryFilters()
            .Include(m => m.Troop)
            .Where(m => !m.IsDeleted);

        if (_currentUser.HasTroopScope && _currentUser.TroopId.HasValue)
            query = query.Where(m => m.TroopId == _currentUser.TroopId.Value);
        else if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
            query = query.Where(m => m.GroupId == _currentUser.GroupId.Value);

        var members = await query
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .ToListAsync();

        var troopGroups = members
            .GroupBy(m => m.Troop?.Name ?? "Unassigned")
            .OrderBy(g => g.Key == "Unassigned" ? "zzz" : g.Key)
            .Select(g => (TroopName: g.Key, Members: g.ToList()))
            .ToList();

        var scope    = _currentUser.IsSystemAdmin ? "All" : (troopGroups.Count == 1 ? troopGroups[0].TroopName : "Group");
        var filename = $"QR-Codes-{DateTime.UtcNow:yyyy-MM-dd}.pdf";

        var doc = new PdfDocument();
        doc.Info.Title = "Member QR Codes";

        AddCover(doc, scope, members.Count, troopGroups.Count);
        foreach (var (troopName, troopMembers) in troopGroups)
            AddTroopPages(doc, troopName, troopMembers);

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return (ms.ToArray(), filename);
    }

    // ── Cover page ─────────────────────────────────────────────────────────────

    private static void AddCover(PdfDocument doc, string scope, int total, int troops)
    {
        var page = doc.AddPage();
        page.Width = PageW; page.Height = PageH;
        using var g = XGraphics.FromPdfPage(page);

        double cy = PageH / 2 - 70;
        g.DrawString("Member QR Codes",
            new XFont("Arial", 32, XFontStyle.Bold), new XSolidBrush(ColPrimDark),
            new XRect(0, cy, PageW, 44), XStringFormats.Center); cy += 50;

        g.DrawString(scope,
            new XFont("Arial", 18), new XSolidBrush(ColPrimary),
            new XRect(0, cy, PageW, 30), XStringFormats.Center); cy += 45;

        g.DrawLine(new XPen(ColBorder), Margin, cy, PageW - Margin, cy); cy += 18;

        g.DrawString($"Exported: {DateTime.Now:MMMM dd, yyyy}",
            new XFont("Arial", 12), new XSolidBrush(ColText2),
            new XRect(0, cy, PageW, 20), XStringFormats.Center); cy += 26;

        g.DrawString($"{total} members  ·  {troops} troop(s)",
            new XFont("Arial", 12), new XSolidBrush(ColText2),
            new XRect(0, cy, PageW, 20), XStringFormats.Center);
    }

    // ── Troop section ──────────────────────────────────────────────────────────

    private void AddTroopPages(PdfDocument doc, string troop, List<Domain.Entities.Member> list)
    {
        double colW        = (PageW - Margin * 2) / Cols;
        int    rowsPerPage = Math.Max(1, (int)Math.Floor((PageH - Margin - HeaderH - FooterH) / CardH));
        int    perPage     = rowsPerPage * (int)Cols;

        for (int start = 0; start < list.Count; start += perPage)
        {
            var page = doc.AddPage();
            page.Width = PageW; page.Height = PageH;
            using var g = XGraphics.FromPdfPage(page);

            // Header band
            g.DrawRectangle(new XSolidBrush(ColPrimDark), 0, 0, PageW, HeaderH);
            DrawText(g, troop, 13, true, XColors.White,
                new XRect(Margin, 0, PageW - Margin * 2, HeaderH), XStringFormats.CenterLeft);
            g.DrawString($"{list.Count} members",
                new XFont("Arial", 9), new XSolidBrush(XColor.FromArgb(0xc5, 0xca, 0xe9)),
                new XRect(0, 0, PageW - Margin, HeaderH), XStringFormats.CenterRight);

            // Member cards
            var chunk = list.Skip(start).Take(perPage).ToList();
            for (int i = 0; i < chunk.Count; i++)
            {
                var m   = chunk[i];
                int col = i % (int)Cols;
                int row = i / (int)Cols;
                double cx = Margin + col * colW;
                double cy = HeaderH + row * CardH + 6;

                g.DrawRectangle(new XPen(ColBorder, 0.5), cx + 2, cy, colW - 4, CardH - 4);

                // QR code
                try
                {
                    var qrPng = _qrCode.GenerateQrCodeImage(m.QrCode);
                    var qrImg = XImage.FromStream(() => new MemoryStream(qrPng));
                    double sz = 90;
                    g.DrawImage(qrImg, cx + (colW - sz) / 2, cy + 8, sz, sz);
                }
                catch { /* skip */ }

                double ty = cy + 105;

                // Name — GDI+ for Arabic, direct text for Latin
                DrawText(g, m.FullName, 8.5, true, ColPrimDark,
                    new XRect(cx + 4, ty, colW - 8, 16), XStringFormats.TopCenter);

                // ID (always ASCII)
                g.DrawString($"#{m.CustomId:D6}",
                    new XFont("Arial", 8), new XSolidBrush(ColPrimary),
                    new XRect(cx + 4, ty + 16, colW - 8, 13), XStringFormats.TopCenter);

                // Grade
                if (!string.IsNullOrWhiteSpace(m.AcademicYear))
                    DrawText(g, m.AcademicYear, 7.5, false, ColText2,
                        new XRect(cx + 4, ty + 29, colW - 8, 13), XStringFormats.TopCenter);
            }

            // Footer
            g.DrawString(troop, new XFont("Arial", 7), new XSolidBrush(ColText2),
                new XRect(Margin, PageH - 20, PageW / 2, 14), XStringFormats.TopLeft);
        }
    }

    // ── Shared text drawing ────────────────────────────────────────────────────

    /// <summary>
    /// Draws text using GDI+ image for Arabic (correct shaping + RTL),
    /// or direct PDF text for Latin/numbers.
    /// </summary>
    private static void DrawText(
        XGraphics g, string text, double ptSize, bool bold,
        XColor color, XRect rect, XStringFormat fmt)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        if (IsArabic(text) && OnWindows)
        {
            // Scale up 3× for crisp rendering at small sizes
            const int scale = 3;
            int pxW = Math.Max(1, (int)(rect.Width  * scale));
            int pxH = Math.Max(1, (int)(rect.Height * scale));

            var sysColor = System.Drawing.Color.FromArgb((int)(color.A * 255), color.R, color.G, color.B);
            var png = GdiRenderText(text, (float)(ptSize * scale * 0.75), bold, sysColor, pxW, pxH);
            if (png != null)
            {
                var img = XImage.FromStream(() => new MemoryStream(png));
                g.DrawImage(img, rect.X, rect.Y, rect.Width, rect.Height);
                return;
            }
        }

        // Latin / fallback
        var style = bold ? XFontStyle.Bold : XFontStyle.Regular;
        g.DrawString(text, new XFont("Arial", ptSize, style), new XSolidBrush(color), rect, fmt);
    }
}
