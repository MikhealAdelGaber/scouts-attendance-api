using ScoutsAttendance.Domain.Common;
using ScoutsAttendance.Domain.Enums;

namespace ScoutsAttendance.Domain.Entities;

public class ReportTemplateCategory : BaseEntity
{
    public Guid         ReportTemplateId   { get; set; }
    public CategoryType CategoryType       { get; set; }
    public string       CategoryName       { get; set; } = string.Empty;

    /// <summary>Weight as a percentage (e.g. 35 = 35%).  All categories must sum to 100.</summary>
    public decimal      Weight             { get; set; }
    public string?      CustomDescription  { get; set; }
    public int          SortOrder          { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────
    public ReportTemplate Template     { get; set; } = null!;
    public ICollection<MemberCustomScore> CustomScores { get; set; } = new List<MemberCustomScore>();
}
