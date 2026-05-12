using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

public class MemberPointCategory : BaseEntity
{
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid?   GroupId     { get; set; }
    public bool    IsGlobal    { get; set; }

    /// <summary>Points awarded automatically when a member is marked Present.</summary>
    public decimal AttendancePresentPoints { get; set; } = 0m;
    /// <summary>Points awarded automatically when a member is marked Late.</summary>
    public decimal AttendanceLatePoints    { get; set; } = 0m;

    public Group?                       Group        { get; set; }
    public ICollection<MemberPoints>    MemberPoints { get; set; } = [];
}
