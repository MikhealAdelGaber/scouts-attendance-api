using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

public class Project : BaseEntity
{
    public string   Name        { get; set; } = string.Empty;
    public string?  Description { get; set; }

    /// <summary>Maximum possible score for this project (e.g. 30, 50, 100).</summary>
    public decimal  MaxScore    { get; set; }

    /// <summary>Group this project belongs to.</summary>
    public Guid     GroupId     { get; set; }

    /// <summary>Null = applies to ALL troops in the group.  Non-null = specific troop only.</summary>
    public Guid?    TroopId     { get; set; }

    public string   CreatedBy   { get; set; } = string.Empty;
    public bool     IsActive    { get; set; } = true;

    // ── Navigation ────────────────────────────────────────────────────────────
    public Group   Group  { get; set; } = null!;
    public Troop?  Troop  { get; set; }
    public ICollection<MemberProjectScore> Scores { get; set; } = new List<MemberProjectScore>();
}
