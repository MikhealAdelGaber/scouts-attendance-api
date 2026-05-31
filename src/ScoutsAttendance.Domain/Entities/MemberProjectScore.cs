using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

public class MemberProjectScore : BaseEntity
{
    public Guid     ProjectId { get; set; }
    public Guid     MemberId  { get; set; }

    /// <summary>Actual score (0 .. Project.MaxScore).</summary>
    public decimal  Score     { get; set; }
    public string?  Notes     { get; set; }
    public string   GradedBy  { get; set; } = string.Empty;
    public DateTime GradedAt  { get; set; } = DateTime.UtcNow;

    // ── Navigation ────────────────────────────────────────────────────────────
    public Project Project { get; set; } = null!;
    public Member  Member  { get; set; } = null!;
}
