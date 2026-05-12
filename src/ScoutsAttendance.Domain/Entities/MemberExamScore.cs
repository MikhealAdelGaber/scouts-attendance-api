using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

/// <summary>End-of-year exam score for a scout member (0-100).</summary>
public class MemberExamScore : BaseEntity
{
    public Guid    MemberId  { get; set; }
    public int     Year      { get; set; }          // Scout year, e.g. 2024
    public decimal Score     { get; set; }           // 0–100
    public string? Notes     { get; set; }
    public Guid    CreatedBy { get; set; }

    public Member Member { get; set; } = null!;
}
