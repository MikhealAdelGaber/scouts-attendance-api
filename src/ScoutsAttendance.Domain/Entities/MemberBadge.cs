using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

/// <summary>An instance of a badge awarded to a specific member.</summary>
public class MemberBadge : BaseEntity
{
    public Guid      MemberId    { get; set; }
    public Guid      BadgeId     { get; set; }
    public DateTime  AwardedDate { get; set; }
    public Guid?     TroopId     { get; set; }   // troop that awarded it
    public string?   TroopName   { get; set; }   // snapshot — survives troop deletion / member transfer
    public Guid?     GroupId     { get; set; }   // group at time of award (for duplicate-per-group check)
    public string?   GroupName   { get; set; }   // snapshot of the group name at award time
    public string    AwardedBy   { get; set; } = string.Empty;  // username from JWT
    public string?   Notes       { get; set; }

    // Navigation
    public Member Member { get; set; } = null!;
    public Badge  Badge  { get; set; } = null!;
    public Troop? Troop  { get; set; }
}
