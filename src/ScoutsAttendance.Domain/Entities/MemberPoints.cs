using ScoutsAttendance.Domain.Common;

namespace ScoutsAttendance.Domain.Entities;

public class MemberPoints : BaseEntity
{
    public Guid     MemberId              { get; set; }
    public Guid?    MemberPointCategoryId { get; set; }   // replaces CategoryId → PointCategory
    public decimal  Points                { get; set; }
    public DateTime Date                  { get; set; } = DateTime.UtcNow;
    public string?  Note                  { get; set; }
    public Guid     AddedBy               { get; set; }
    public Guid?    AttendanceRecordId    { get; set; }

    public Member               Member          { get; set; } = null!;
    public MemberPointCategory? Category        { get; set; }
    public AttendanceRecord?    AttendanceRecord { get; set; }
}
