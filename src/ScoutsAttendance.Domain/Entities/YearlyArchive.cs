using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

/// <summary>
/// Header record for an end-of-year archive snapshot.
/// One row per year reset; holds aggregate totals and metadata.
/// </summary>
public class YearlyArchive : BaseEntity
{
    /// <summary>Academic year label, e.g. "2024-2025".</summary>
    public string   ArchiveYear  { get; set; } = string.Empty;

    public DateTime ArchivedAt   { get; set; } = DateTime.UtcNow;

    /// <summary>Username of the SystemAdmin who triggered the reset.</summary>
    public string   ArchivedBy   { get; set; } = string.Empty;

    public int      TotalMembers { get; set; }
    public int      TotalGroups  { get; set; }

    public ICollection<YearlyMemberArchive> Members { get; set; } = new List<YearlyMemberArchive>();
}
