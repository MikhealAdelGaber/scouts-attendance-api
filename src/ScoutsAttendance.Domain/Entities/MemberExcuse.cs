using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

/// <summary>
/// Permanent or time-limited excuse for a member.
/// While an excuse is active, absences are automatically treated as Excused
/// and the member still receives attendance points.
/// </summary>
public class MemberExcuse : BaseEntity
{
    public Guid      MemberId  { get; set; }
    public DateTime  StartDate { get; set; }
    public DateTime? EndDate   { get; set; }   // null = open-ended / permanent
    public string?   Reason    { get; set; }
    public bool      IsActive  { get; set; } = true;
    public Guid      GrantedBy { get; set; }

    public Member Member { get; set; } = null!;
}
