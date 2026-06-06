using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.Infrastructure.Services;

/// <summary>
/// Generates a print-ready A4 PDF of all member QR codes, grouped by Troop.
/// Uses PdfSharpCore (pure managed — works on win-x86, win-x64, linux, etc.)
/// </summary>
public sealed class QrPdfExportService : IQrPdfExportService
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IQrCodeService      _qrCode;

    private const double PageW   = 595;   // A4 pt
    private const double PageH   = 842;
    private const double Margin  = 36;
    private const double Cols    = 3;
    private const double CardH   = 170;
    private const double HeaderH = 40;
    private const double FooterH = 22;

    private static readonly XColor ColPrimDark = XColor.FromArgb(0x1a, 0x23, 0x7e);
    private static readonly XColor ColPrimary  = XColor.FromArgb(0x3f, 0x51, 0xb5);
    private static readonly XColor ColText2    = XColor.FromArgb(0x5a, 0x63, 0x79);
    private static readonly XColor ColBorder   = XColor.FromArgb(0xe0, 0xe0, 0xe0);

    /// <summary>
    /// Fixes Arabic/RTL text for LTR PDF engines (PdfSharpCore has no bidi support).
    /// Reverses word order and each word's characters so Arabic reads correctly in PDF.
    /// Leaves Latin-only strings unchanged.
    /// </summary>
    private static string FixRtl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? "";
        // Check if the string contains Arabic characters
        if (!text.Any(c => c >= '؀' && c <= 'ۿ')) return text;

        // Split into words, reverse each word's chars, then reverse word order
        // so the whole string reads RTL when drawn LTR by PdfSharpCore
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var fixedWords = words.Select(w => new string(w.Reverse().ToArray()));
        return string.Join(" ", fixedWords.Reverse());
    }

    public QrPdfExportService(IUnitOfWork uow, ICurrentUserService currentUser, IQrCodeService qrCode)
    {
        _uow = uow; _currentUser = currentUser; _qrCode = qrCode;
    }

    public async Task<(byte[] Bytes, string Filename)> ExportAsync()
    {
        var query = _uow.Members.Query().IgnoreQueryFilters()
            .Include(m => m.Troop).Where(m => !m.IsDeleted);

        if (_currentUser.HasTroopScope && _currentUser.TroopId.HasValue)
            query = query.Where(m => m.TroopId == _currentUser.TroopId.Value);
        else if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
            query = query.Where(m => m.GroupId == _currentUser.GroupId.Value);

        var members = await query.OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ToListAsync();

        var groups = members
            .GroupBy(m => m.Troop?.Name ?? "Unassigned")
            .OrderBy(g => g.Key == "Unassigned" ? "zzz" : g.Key)
            .Select(g => (g.Key, g.ToList()))
            .ToList();

        var scope    = _currentUser.IsSystemAdmin ? "All" : (groups.Count == 1 ? groups[0].Key : "Group");
        var filename = $"QR-Codes-{DateTime.UtcNow:yyyy-MM-dd}.pdf";

        var doc = new PdfDocument();
        doc.Info.Title = "Member QR Codes";

        AddCover(doc, scope, members.Count, groups.Count);
        foreach (var (troop, list) in groups)
            AddTroopSection(doc, troop, list);

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return (ms.ToArray(), filename);
    }

    private static void AddCover(PdfDocument doc, string scope, int total, int troops)
    {
        var page = doc.AddPage(); page.Width = PageW; page.Height = PageH;
        using var g = XGraphics.FromPdfPage(page);
        var f32 = new XFont("Arial", 32, XFontStyle.Bold);
        var f18 = new XFont("Arial", 18, XFontStyle.Regular);
        var f12 = new XFont("Arial", 12, XFontStyle.Regular);
        double cy = PageH / 2 - 70;
        g.DrawString("Member QR Codes", f32, new XSolidBrush(ColPrimDark), new XRect(0, cy, PageW, 44), XStringFormats.Center); cy += 50;
        g.DrawString(scope, f18, new XSolidBrush(ColPrimary), new XRect(0, cy, PageW, 30), XStringFormats.Center); cy += 45;
        g.DrawLine(new XPen(ColBorder), Margin, cy, PageW - Margin, cy); cy += 18;
        g.DrawString($"Exported: {DateTime.Now:MMMM dd, yyyy}", f12, new XSolidBrush(ColText2), new XRect(0, cy, PageW, 20), XStringFormats.Center); cy += 26;
        g.DrawString($"{total} members  ·  {troops} troop(s)", f12, new XSolidBrush(ColText2), new XRect(0, cy, PageW, 20), XStringFormats.Center);
    }

    private void AddTroopSection(PdfDocument doc, string troop, List<Domain.Entities.Member> list)
    {
        double colW        = (PageW - Margin * 2) / Cols;
        int    rowsPerPage = Math.Max(1, (int)Math.Floor((PageH - Margin - HeaderH - FooterH) / CardH));
        int    perPage     = rowsPerPage * (int)Cols;

        for (int start = 0; start < list.Count; start += perPage)
        {
            var page = doc.AddPage(); page.Width = PageW; page.Height = PageH;
            using var g = XGraphics.FromPdfPage(page);

            var fHead = new XFont("Arial", 13, XFontStyle.Bold);
            var fSub  = new XFont("Arial",  9, XFontStyle.Regular);
            var fName = new XFont("Arial",  8, XFontStyle.Bold);
            var fId   = new XFont("Arial",  8, XFontStyle.Regular);
            var fGrd  = new XFont("Arial", 7.5, XFontStyle.Regular);

            // Header
            g.DrawRectangle(new XSolidBrush(ColPrimDark), 0, 0, PageW, HeaderH);
            g.DrawString(FixRtl(troop), fHead, new XSolidBrush(XColors.White), new XRect(Margin, 0, PageW - Margin * 2, HeaderH), XStringFormats.CenterLeft);
            g.DrawString($"{list.Count} members", fSub, new XSolidBrush(XColor.FromArgb(0xc5, 0xca, 0xe9)), new XRect(0, 0, PageW - Margin, HeaderH), XStringFormats.CenterRight);

            // Cards
            var chunk = list.Skip(start).Take(perPage).ToList();
            for (int i = 0; i < chunk.Count; i++)
            {
                var m   = chunk[i];
                int col = i % (int)Cols;
                int row = i / (int)Cols;
                double x = Margin + col * colW;
                double y = HeaderH + row * CardH + 6;

                g.DrawRectangle(new XPen(ColBorder, 0.5), x + 2, y, colW - 4, CardH - 4);

                try
                {
                    var qrPng = _qrCode.GenerateQrCodeImage(m.QrCode);
                    var qrImg = XImage.FromStream(() => new MemoryStream(qrPng));
                    double sz = 90, qx = x + (colW - sz) / 2;
                    g.DrawImage(qrImg, qx, y + 8, sz, sz);
                }
                catch { /* skip on error */ }

                double ty = y + 104;
                g.DrawString(FixRtl(m.FullName), fName, new XSolidBrush(ColPrimDark), new XRect(x + 4, ty, colW - 8, 14), XStringFormats.TopCenter);
                g.DrawString($"#{m.CustomId:D6}", fId, new XSolidBrush(ColPrimary), new XRect(x + 4, ty + 14, colW - 8, 12), XStringFormats.TopCenter);
                if (!string.IsNullOrWhiteSpace(m.AcademicYear))
                    g.DrawString(FixRtl(m.AcademicYear), fGrd, new XSolidBrush(ColText2), new XRect(x + 4, ty + 27, colW - 8, 12), XStringFormats.TopCenter);
            }

            // Footer
            g.DrawString(troop, new XFont("Arial", 7, XFontStyle.Regular), new XSolidBrush(ColText2), new XRect(Margin, PageH - 20, PageW / 2, 14), XStringFormats.TopLeft);
        }
    }
}
