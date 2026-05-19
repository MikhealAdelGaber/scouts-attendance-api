using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Application.Services;
using ScoutsAttendance.Domain.Entities;

namespace ScoutsAttendance.Infrastructure.Services;

/// <summary>
/// Generates a print-ready A4 PDF of all member QR codes, grouped by Troop.
/// Each QR encodes the member's "SCOUT-XXXXXX" token (same as the scanning flow).
/// Uses QuestPDF (community licence) + QRCoder (already in the project).
/// </summary>
public sealed class QrPdfExportService : IQrPdfExportService
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IQrCodeService      _qrCode;
    private readonly IPhotoService       _photo;

    // ── Brand colours (match the Angular app) ────────────────────────────────
    private const string PrimaryDark  = "#1a237e";
    private const string Primary      = "#3f51b5";
    private const string TextPrimary  = "#1a1a2e";
    private const string TextSecond   = "#5a6379";
    private const string TextHint     = "#9e9e9e";
    private const string BorderColor  = "#e0e0e0";
    private const string White        = "#ffffff";

    public QrPdfExportService(
        IUnitOfWork uow,
        ICurrentUserService currentUser,
        IQrCodeService qrCode,
        IPhotoService photo)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _qrCode      = qrCode;
        _photo       = photo;
    }

    public async Task<(byte[] Bytes, string Filename)> ExportAsync()
    {
        // ── 1. Load members (scoped to caller's role) ─────────────────────────
        var query = _uow.Members.Query()
            .IgnoreQueryFilters()                    // don't let soft-deleted Troop filter out members
            .Include(m => m.Troop)
            .Where(m => !m.IsDeleted);

        if (_currentUser.HasTroopScope && _currentUser.TroopId.HasValue)
        {
            // AttendanceOnly / scoped GroupLeader → single troop
            query = query.Where(m => m.TroopId == _currentUser.TroopId.Value);
        }
        else if (!_currentUser.IsSystemAdmin && _currentUser.GroupId.HasValue)
        {
            // GroupLeader without explicit troop → whole group
            query = query.Where(m => m.GroupId == _currentUser.GroupId.Value);
        }
        // SystemAdmin → no additional filter (all members)

        var members = await query
            .OrderBy(m => m.LastName)
            .ThenBy(m => m.FirstName)
            .ToListAsync();

        // ── 2. Group by Troop (unassigned last) ───────────────────────────────
        var troopGroups = members
            .GroupBy(m => new
            {
                TroopId   = m.TroopId,
                TroopName = m.Troop?.Name ?? "Unassigned"
            })
            .OrderBy(g => g.Key.TroopName == "Unassigned" ? "￿" : g.Key.TroopName)
            .Select(g => (
                TroopName : g.Key.TroopName,
                Members   : g.OrderBy(m => m.LastName).ThenBy(m => m.FirstName).ToList()
            ))
            .ToList();

        // ── 3. Determine scope label & filename ───────────────────────────────
        var scopeLabel = _currentUser.IsSystemAdmin
            ? "All Troops"
            : (troopGroups.Count == 1 ? troopGroups[0].TroopName : "Group");

        var safeScope = string.Concat(scopeLabel.Split(Path.GetInvalidFileNameChars()));
        var filename  = $"QR-Codes-{safeScope}-{DateTime.UtcNow:yyyy-MM-dd}.pdf";

        // ── 4. Pre-fetch profile photos (best-effort, in parallel) ────────────
        var photoMap = new Dictionary<Guid, byte[]?>();
        await Task.WhenAll(members.Select(async m =>
        {
            if (!string.IsNullOrWhiteSpace(m.ProfileImageUrl))
            {
                try { photoMap[m.Id] = await _photo.GetPhotoBytesAsync(m.ProfileImageUrl); }
                catch { photoMap[m.Id] = null; }
            }
        }));

        // ── 5. Build PDF ──────────────────────────────────────────────────────
        var pdfBytes = Document.Create(container =>
        {
            BuildCoverPage(container, scopeLabel, members.Count, troopGroups.Count);

            foreach (var (troopName, troopMembers) in troopGroups)
                BuildTroopPage(container, troopName, troopMembers, photoMap);
        })
        .GeneratePdf();

        return (pdfBytes, filename);
    }

    // ── Cover page ────────────────────────────────────────────────────────────

    private static void BuildCoverPage(
        IDocumentContainer container,
        string scopeLabel,
        int totalMembers,
        int totalTroops)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);

            page.Content()
                .AlignCenter()
                .AlignMiddle()
                .Column(col =>
                {
                    col.Spacing(16);

                    // Title
                    col.Item()
                        .Text("Member QR Codes")
                        .Bold().FontSize(36).FontColor(PrimaryDark);

                    // Scope
                    col.Item()
                        .Text(scopeLabel)
                        .FontSize(20).FontColor(Primary);

                    // Divider
                    col.Item().PaddingVertical(8)
                        .LineHorizontal(1).LineColor(BorderColor);

                    // Export date
                    col.Item()
                        .Text($"Exported: {DateTime.Now:MMMM dd, yyyy}")
                        .FontSize(13).FontColor(TextHint);

                    // Counts
                    col.Item().PaddingTop(4)
                        .Text($"{totalMembers} members  ·  {totalTroops} troop(s)")
                        .FontSize(12).FontColor(TextSecond);
                });
        });
    }

    // ── Per-troop section page(s) ─────────────────────────────────────────────

    private void BuildTroopPage(
        IDocumentContainer container,
        string troopName,
        List<Member> troopMembers,
        Dictionary<Guid, byte[]?> photoMap)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(1.2f, Unit.Centimetre);
            page.MarginTop(0.4f, Unit.Centimetre);
            page.MarginBottom(1.2f, Unit.Centimetre);

            // ── Header band ──────────────────────────────────────────────────
            page.Header()
                .Background(PrimaryDark)
                .Padding(10)
                .Row(row =>
                {
                    row.RelativeItem()
                        .AlignMiddle()
                        .Text(troopName)
                        .Bold().FontSize(15).FontColor(White);

                    row.AutoItem()
                        .AlignMiddle()
                        .AlignRight()
                        .Text($"{troopMembers.Count} member{(troopMembers.Count != 1 ? "s" : "")}")
                        .FontSize(10).FontColor($"#c5cae9");
                });

            // ── 3-column QR grid ─────────────────────────────────────────────
            page.Content()
                .PaddingTop(10)
                .Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                    });

                    foreach (var member in troopMembers)
                    {
                        photoMap.TryGetValue(member.Id, out var photoBytes);
                        BuildMemberCell(table, member, photoBytes);
                    }

                    // Pad the last row with empty cells so the grid stays aligned
                    var remainder = troopMembers.Count % 3;
                    if (remainder != 0)
                        for (int i = 0; i < 3 - remainder; i++)
                            table.Cell().MinHeight(130);
                });

            // ── Footer ───────────────────────────────────────────────────────
            page.Footer()
                .PaddingTop(4)
                .Row(row =>
                {
                    row.RelativeItem()
                        .AlignBottom()
                        .Text(troopName)
                        .FontSize(7.5f).FontColor(TextHint);

                    row.RelativeItem()
                        .AlignBottom()
                        .AlignRight()
                        .Text(txt =>
                        {
                            txt.Span("Page ").FontSize(7.5f).FontColor(TextHint);
                            txt.CurrentPageNumber().FontSize(7.5f).FontColor(TextHint);
                            txt.Span(" of ").FontSize(7.5f).FontColor(TextHint);
                            txt.TotalPages().FontSize(7.5f).FontColor(TextHint);
                        });
                });
        });
    }

    // ── Single member card cell ───────────────────────────────────────────────

    private void BuildMemberCell(TableDescriptor table, Member member, byte[]? photoBytes)
    {
        // Generate QR PNG bytes — encodes "SCOUT-XXXXXX"
        var qrBytes = _qrCode.GenerateQrCodeImage(member.QrCode);

        table.Cell()
            .Border(0.5f)
            .BorderColor(BorderColor)
            .Padding(7)
            .Column(col =>
            {
                col.Spacing(3);

                // ── Profile photo (optional, 50×50 circle-like square) ──────
                if (photoBytes is { Length: > 0 })
                {
                    col.Item()
                        .AlignCenter()
                        .Width(50)
                        .Height(50)
                        .Image(photoBytes)
                        .FitArea();
                }

                // QR image — 90pt ≈ 3.2 cm, well above the 3 cm minimum
                col.Item()
                    .AlignCenter()
                    .Width(90)
                    .Image(qrBytes);

                // Member full name
                col.Item()
                    .AlignCenter()
                    .PaddingTop(2)
                    .Text(member.FullName)
                    .Bold().FontSize(8.5f).FontColor(TextPrimary);

                // 6-digit scout ID
                col.Item()
                    .AlignCenter()
                    .Text($"#{member.CustomId:D6}")
                    .FontSize(8f).FontColor(Primary);

                // Academic grade (optional)
                if (!string.IsNullOrWhiteSpace(member.AcademicYear))
                    col.Item()
                        .AlignCenter()
                        .Text(member.AcademicYear)
                        .FontSize(7.5f).FontColor(TextSecond);
            });
    }
}
