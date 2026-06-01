using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

/// <summary>
/// Manually-entered score (0-100) for a Custom category in a report template.
/// Unique per (ReportTemplateCategoryId, MemberId).
/// </summary>
public class MemberCustomScore : BaseEntity
{
    public Guid     ReportTemplateCategoryId { get; set; }
    public Guid     MemberId                { get; set; }

    /// <summary>Raw score 0-100 (before applying the category weight).</summary>
    public decimal  Score      { get; set; }
    public string?  Notes      { get; set; }
    public string   EnteredBy  { get; set; } = string.Empty;
    public DateTime EnteredAt  { get; set; } = DateTime.UtcNow;

    // ── Navigation ────────────────────────────────────────────────────────────
    public ReportTemplateCategory Category { get; set; } = null!;
    public Member                 Member   { get; set; } = null!;
}
